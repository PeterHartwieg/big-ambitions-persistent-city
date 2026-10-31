using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

sealed class Paths : IRoutePlanner, IFoodWalkingPace
{
    public double WalkingMetresPerMinute=>120;
    public int Calls;
    public Route Plan(Place a,Place b,double at,TravelPreferences p)
    {Calls++;return new Route {Legs=new() {new RouteLeg {From=a.Position,To=b.Position,Minutes=1,Mode=TravelMode.Walk}}};}
}
sealed class Market : IFoodMarket, ILocalFoodMarket, IFoodCandidateMarket
{
    public readonly Dictionary<string,FoodBusiness> Shops=new(StringComparer.Ordinal);
    public double EarliestMinute=>0;
    public IReadOnlyList<FoodBusiness> Read(double at)=>throw new Exception("Unbounded supply read");
    public IReadOnlyList<FoodBusiness> ReadLocal(Place p,double at,double tolerance,int max)=>throw new Exception("Legacy shortlist");
    public FoodBusiness? ReadBusiness(string id,double at)=>Shops.GetValueOrDefault(id);
    public FoodBusiness? CaptureBusiness(string id,double at)=>ReadBusiness(id,at);
    public bool IsCandidate(FoodScope scope)=>Shops.ContainsKey(scope.BusinessId);
    public FoodSale Buy(ResidentMeal meal,double at)=>throw new Exception("No checkout in decision benchmark");
}
static class Program
{
    static (CitySimulator city,Paths paths) Ready(int seed,int count,bool nativeIds)
    {
        var places=new List<Place> {new() {Id="home",Name="Home",Kind=PlaceKind.Home,Capacity=1}};
        places.AddRange(Enumerable.Range(0,count).Select(i=>new Place {Id="s"+i,Name="Shop",Kind=PlaceKind.Shop,Position=new(1+i,0,0)}));
        var state=Population.Create(new[]{new CitizenIdentity {Id="r",Name="Resident",Age=30,Persona=Persona.Foodie}},places,"choice-regression","0.40.4",seed,1440);
        var p=state.Citizens[0];p.NextEventMinute=p.ActivityUntilMinute=2870;
        state.Food=new FoodCommerceState {Shops=places.Skip(1).Select((s,i)=>new FoodScope {PlaceId=s.Id,
            BusinessId=nativeIds ? "business:ba%3Astreet_martinlutherkingjrblvd:"+i+":1:ba%3Abusiness_fastfoodrestaurant:player" : "b"+i,ProductId=FoodProducts.Croissant,
            AdditionalProductIds=new() {FoodProducts.Burger,FoodProducts.HotDog}}).ToList()};
        var market=new Market();
        foreach(var s in state.Food.Shops) market.Shops.Add(s.BusinessId,new FoodBusiness {Id=s.BusinessId,PlaceId=s.PlaceId,MealsPerHour=60,
            Hours=Enumerable.Range(0,7).Select(d=>new OpeningWindow {Day=d,FromMinute=0,ToMinute=1440}).ToList(),
            Offers=FoodProducts.Ids.Select(id=>new FoodOffer {ProductId=id,Price=Money.FromCents(100),Units=100}).ToList()});
        var paths=new Paths();var city=new CitySimulator(state,paths);city.AttachFoodMarket(market);state.Food.ChoiceStartsDay=1;
        return (city,paths);
    }
    static object Bench(int shops,bool nativeIds=false)
    {
        var totals=new List<double>();var units=new List<double>();var allocations=new List<long>();var policyCosts=new List<double>();int calls=0;
        var evaluated=Enumerable.Range(0,8).SelectMany(i=>FoodProducts.Ids.Select(id=>new EvaluatedFoodOffer("b"+i,id,Money.FromCents(100),1,0,12,90,75,60,MealKind.Lunch))).ToArray();
        for(int i=0;i<600;i++)
        {
            var (city,paths)=Ready(i,shops,nativeIds);double at=city.State.Food!.Residents[0].DesiredAt;
            long before=GC.GetAllocatedBytesForCurrentThread();long start=Stopwatch.GetTimestamp();
            bool caught;int step=0;double captureMs=0;
            do
            {
                long unit=Stopwatch.GetTimestamp();caught=city.AdvanceFrameSlice(at,1).CaughtUp;
                double unitMs=Stopwatch.GetElapsedTime(unit).TotalMilliseconds;
                if(step++==0) captureMs=unitMs;
                if(i>=88) units.Add(unitMs);
            }while(!caught);
            double elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            calls=paths.Calls;
            if(calls!=2*Math.Min(8,shops)) throw new Exception("Route ceiling changed");
            if(city.State.Food.Residents[0].Status!=MealStatus.Travelling) throw new Exception("No feasible winner");
            var m=city.State.Food.Residents[0];var draw=new FoodDrawContext(i,m.ResidentId,m.Day,m.Kind,m.LastContextKey,1);
            var settings=new FoodChoiceSettings();long pick=Stopwatch.GetTimestamp();
            FoodChoicePolicy.Select(evaluated,city.State.Citizens[0].Traits!,m.Allowance,draw,settings,false);
            double pickMs=Stopwatch.GetElapsedTime(pick).TotalMilliseconds;
            if(i>=88) {totals.Add(elapsed);allocations.Add(bytes);policyCosts.Add(captureMs+pickMs);}
        }
        totals.Sort();units.Sort();allocations.Sort();policyCosts.Sort();
        return new {shops,nativeIds,samples=totals.Count,fullMs=new {p50=totals[totals.Count/2],p95=totals[(int)(totals.Count*.95)],max=totals[^1]},
            continuationUnitMs=new {p95=units[(int)(units.Count*.95)],max=units[^1]},shortlistCapturePlusPureSamplingMs=new {p95=policyCosts[(int)(policyCosts.Count*.95)],max=policyCosts[^1]},bytesPerDecision=allocations[allocations.Count/2],logicalRoutes=calls};
    }
    static void Main()
    {
        var d=new FoodDrawContext(123,"r|:42",7,MealKind.Lunch,"trip:abc",3);
        if(d.ShopHash("b:9")!=2746120566u || d.OfferHash("b:9",FoodProducts.Burger)!=360816019u || d.OfferHash("","")!=3912922371u)
            throw new Exception("Version-1 draw vectors changed");
        Console.WriteLine(JsonSerializer.Serialize(new {bench=new[]{Bench(8),Bench(64),Bench(1024)},nativeIdBench=new[]{Bench(8,true),Bench(64,true),Bench(1024,true)}}));
    }
}
