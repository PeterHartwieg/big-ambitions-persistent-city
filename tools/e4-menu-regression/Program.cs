using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using BigAmbitions.Items;
using Buildings.Retail.Simulation;

static class Program
{
    static int checks;
    static void Check(bool pass,string message) { if(!pass) throw new Exception(message); checks++; }
    static readonly string[] Products=Enumerable.Range(7,8).Select(i=>FoodMenuPolicy.Product(i)!).ToArray();
    static object Bench(int count,Action action)
    {
        for(int i=0;i<20;i++) action();
        var ms=new double[count];long allocation=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<count;i++) {long start=Stopwatch.GetTimestamp();action();ms[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
        allocation=GC.GetAllocatedBytesForCurrentThread()-allocation;Array.Sort(ms);
        return new { samples=count,p50=ms[count/2],p95=ms[(int)(count*.95)],max=ms[^1],spikes30=ms.Count(n=>n>=30),bytesPerCall=allocation/count };
    }
    sealed class Routes : IRoutePlanner
    {
        public int Calls;
        public Route? Plan(Place from,Place to,double minute,TravelPreferences preferences)
        { Calls++;return new Route{Legs={new RouteLeg{From=from.Position,To=to.Position,Mode=TravelMode.Walk,Minutes=1}}}; }
    }
    sealed class Market : IFoodMarket
    {
        public IReadOnlyList<FoodBusiness> Shops=Array.Empty<FoodBusiness>();
        public double EarliestMinute=>0;
        public IReadOnlyList<FoodBusiness> Read(double minute)=>Shops;
        public FoodSale Buy(ResidentMeal meal,double minute)=>throw new Exception("No native checkout in envelope fixture.");
    }
    static FoodShopDay Row(int day,int shop,string generation,int width)
    {
        var row=new FoodShopDay{Day=day,PlaceId="s"+shop,BusinessId=generation,Purchases=width,Revenue=Money.FromCents(width),
            PurchaseMeals=new long[]{width,0,0,0},BuyerDistricts={new FoodBuyerDistrict{DistrictId="a",Purchases=width}}};
        row.PurchaseHours[0]=width;
        foreach(string product in Products.Take(width)) row.ProductSales.Add(new FoodProductSales{ProductId=product,Purchases=1,Revenue=Money.FromCents(1),PurchaseMeals=new long[]{1,0,0,0}});
        return row;
    }
    static CityState City(int width,int shops=8)
    {
        var places=new List<Place>{new Place{Id="home",Name="Home",Kind=PlaceKind.Home,Capacity=1}};
        places.AddRange(Enumerable.Range(0,shops).Select(i=>new Place{Id="s"+i,Name="Shop",Kind=PlaceKind.Shop,Position=new Position(i+1,0,0)}));
        var state=Population.Create(new[]{new CitizenIdentity{Id="person",Name="Person",Age=30}},places,"e4-test", "test",957,14*1440);
        state.Food=new FoodCommerceState { ChoiceStartsDay=15,AwarenessStartsDay=15 };
        state.Food.Shops.AddRange(Enumerable.Range(0,shops).Select(i=>new FoodScope{PlaceId="s"+i,BusinessId="b"+i,ProductId=Products[0],AdditionalProductIds=Products.Skip(1).Take(width-1).ToList()}));
        var city=new CitySimulator(state,new Routes());city.AttachFoodMarket(new Market());
        return state;
    }
    static object Selection(int width)
    {
        var offers=Enumerable.Range(0,8).SelectMany(shop=>Products.Take(width).Select(product=>new EvaluatedFoodOffer("b"+shop,product,Money.FromCents(100),1,0,4,60,50,50,MealKind.Lunch,0,FoodMenuPolicy.Taste(957,"person",FoodMenuPolicy.Code(product))))).ToArray();
        var traits=ResidentTraits.Generate(957,"person");var settings=new FoodChoiceSettings{FoodMenuMaximumOffersPerShop=width};var draw=new FoodDrawContext(957,"person",14,MealKind.Lunch,"test",1);
        var result=FoodChoicePolicy.Select(offers,traits,Money.FromCents(1000),draw,settings,false);
        Check(result.Index>=0 && offers.Length==8*width,"one product from bounded evaluations");
        return new {width,evaluations=offers.Length,routeSlots=8*2,measurement=Bench(1001,()=>FoodChoicePolicy.Select(offers,traits,Money.FromCents(1000),draw,settings,false))};
    }
    static object LiveDefault()
    {
        var offers=Enumerable.Range(0,8).SelectMany(shop=>new[]{FoodProducts.Croissant,FoodProducts.Burger,FoodProducts.HotDog}.Select(p=>
            new EvaluatedFoodOffer("b"+shop,p,Money.FromCents(100+shop*10),shop+1,shop%3,10+shop,60,70,40,MealKind.Lunch))).ToArray();
        var settings=new FoodChoiceSettings();var allowance=Money.FromCents(1000);
        for(int seed=0;seed<1000;seed++)
        {
            var traits=ResidentTraits.Generate(seed,"consumer:2");var draw=new FoodDrawContext(seed,"consumer:2",14,MealKind.Lunch,"test",1);
            foreach(var offer in offers) Check(BitConverter.DoubleToInt64Bits(FoodChoicePolicyBefore.Weight(offer,traits,allowance,settings))==BitConverter.DoubleToInt64Bits(FoodChoicePolicy.Weight(offer,traits,allowance,settings)),"live product weight bit identity");
            var old=FoodChoicePolicyBefore.Select(offers,traits,allowance,draw,settings,true);var next=FoodChoicePolicy.Select(offers,traits,allowance,draw,settings,true);
            Check(JsonConvert.SerializeObject(old)==JsonConvert.SerializeObject(next),"live default selection byte identity");
        }
        var t=ResidentTraits.Generate(957,"consumer:2");var d=new FoodDrawContext(957,"consumer:2",14,MealKind.Lunch,"test",1);
        return new{offers=offers.Length,seeds=1000,before=Bench(1001,()=>FoodChoicePolicyBefore.Select(offers,t,allowance,d,settings,true)),after=Bench(1001,()=>FoodChoicePolicy.Select(offers,t,allowance,d,settings,true))};
    }
    static object Captured(int width)
    {
        var state=City(width);state.Food!.ChoiceStartsDay=14;state.Food.AwarenessStartsDay=15;
        state.Citizens[0].NextEventMinute=state.Citizens[0].ActivityUntilMinute=15*1440-10;
        var market=new Market{Shops=state.Food.Shops.Select(scope=>new FoodBusiness{Id=scope.BusinessId,PlaceId=scope.PlaceId,MealsPerHour=60,
            Hours=Enumerable.Range(0,7).Select(day=>new OpeningWindow{Day=day,FromMinute=0,ToMinute=1440}).ToList(),
            Offers=scope.Products().Select(p=>new FoodOffer{ProductId=p,Price=Money.FromCents(100),Units=100}).ToList()}).ToArray()};
        var routes=new Routes();var city=new CitySimulator(state,routes);city.AttachFoodMarket(market,new FoodChoiceSettings{FoodMenuMaximumOffersPerShop=width});
        var meal=state.Food.Residents.Single();
        while(meal.Decision==null) city.AdvanceTo(meal.DesiredAt,1);
        Check(meal.Decision.Shops.Count==8 && meal.Decision.Shops.All(s=>s.Offers.Count==width),"complete bounded A3 capture");
        int boundaries=0;
        while(meal.Decision!=null)
        {
            var held=meal.Decision;Check(held.QuotesDone<=16,"quote bound independent of offer width");
            string wire=SaveCodec.Write(city.State);var read=SaveCodec.Read(wire);Check(read.Status==SaveReadStatus.Loaded,"held menu roundtrip: "+read.Error);
            Check(SaveCodec.Write(read.State!)==wire,"interrupted quote replay preserves exact menu");
            city=new CitySimulator(read.State!,routes);
            // Lowering live settings cannot discard or recapture this held image.
            city.AttachFoodMarket(market,new FoodChoiceSettings{FoodMenuMaximumOffersPerShop=1});
            meal=city.State.Food!.Residents.Single();city.AdvanceTo(meal.DesiredAt,1);boundaries++;
        }
        Check(routes.Calls<=16,"at most sixteen decision route calls");
        return new{width,boundaries,routeCalls=routes.Calls};
    }
    static ItemInstance Stock(string product,int amount)=>new(){id=product,ItemCached=new Item(),cargoInstances=new(){new Cargo{itemName=product,amount=amount}}};
    static object Supply()
    {
        var reg=new BuildingRegistration();reg.Counters.Add(new PointOfSale{instance=Stock("bag",1)});
        foreach(string product in Products) reg.Producers[product]=new(){Stock(product,20)};
        var eight=FoodSupplyReader.ReadMenu(reg,Products);
        Check(eight.Count==8 && eight.Values.All(v=>v.Units==1 && v.BagUnits==1 && v.MealsPerHour==15),"eight offers use ONE bag/counter pool");
        reg.Counters[0].instance.cargoInstances![0].amount=0;
        Check(FoodSupplyReader.ReadMenu(reg,Products).Values.All(v=>v.Units==0 && v.MealsPerHour==0),"shared bag shortage blocks all products");
        reg.Counters[0].instance.cargoInstances![0].amount=100;
        var four=Products.Take(4).ToArray();
        string before=JsonConvert.SerializeObject(FoodSupplyReaderBefore.ReadMenu(reg,four));
        Check(before==JsonConvert.SerializeObject(FoodSupplyReader.ReadMenu(reg,four)),"four-offer supply byte identical to 51ce709");
        Check(Throws(()=>FoodSupplyReader.ReadMenu(reg,new[]{Products[0],Products[0]})),"duplicates refuse");
        Check(Throws(()=>FoodSupplyReader.ReadMenu(reg,Products.Append("test:ninth"))),"ninth offer refuses before setup");
        return new {fourBefore=Bench(1001,()=>FoodSupplyReaderBefore.ReadMenu(reg,four)),fourAfter=Bench(1001,()=>FoodSupplyReader.ReadMenu(reg,four)),eightAfter=Bench(1001,()=>FoodSupplyReader.ReadMenu(reg,Products))};
    }
    static bool Throws(Action action) {try{action();return false;}catch(ArgumentException){return true;}catch(SaveValidationException){return true;}}
    static object History(int width)
    {
        var state=City(width,1024);
        for(int day=1;day<=14;day++) for(int shop=0;shop<1024;shop++) state.Food!.History.Add(Row(day,shop,"b"+shop,width));
        string wire=SaveCodec.Write(state);var read=SaveCodec.Read(wire);
        Check(read.Status==SaveReadStatus.Loaded,"history allocation roundtrip: "+read.Error);
        Check(wire==SaveCodec.Write(read.State!),"history no truncation or amount change");
        var counts=SaveCodec.CountRecords(state,wire);
        Check(counts.Semantic>=14*1024*(1+width) && counts.Streaming>=14*1024*(1+width),"both counters charge each row/cell");
        state.Food!.History.Add(Row(14,0,"churn",width));
        string churn=SaveCodec.Write(state);Check(SaveCodec.Read(churn).State!.Food!.History.Count==14337,"churn beyond 1024 generations/day preserved");
        var data=new Dictionary<string,string>{{SaveStore.StateKey,churn}};
        state.Food.History[0].ProductSales[0].Purchases++;
        Check(!SaveStore.WriteTo(data,state).Success && data[SaveStore.StateKey]==churn,"broken sum refuses atomically");
        state.Food.History[0].ProductSales[0].Purchases--;
        state.Food.History[0].ProductSales.Add(new FoodProductSales{ProductId="test:ninth",Purchases=1});
        Check(!SaveStore.WriteTo(data,state).Success && data[SaveStore.StateKey]==churn,"oversized/unsupported history preserved on failure");
        state.Food.History[0].ProductSales.RemoveAt(width);
        state.Food.History.RemoveAt(14336); // churn is tested separately; timing uses the exact named allocation
        return new {width,rows=14336,cells=14336*width,characters=wire.Length,utf8=Encoding.UTF8.GetByteCount(wire),counts,
            update=Bench(101,()=>{foreach(var row in state.Food.History){row.Purchases++;row.PurchaseHours[0]++;row.PurchaseMeals[0]++;row.Revenue+=Money.FromCents(1);var p=row.ProductSales[0];p.Purchases++;p.PurchaseMeals[0]++;p.Revenue+=Money.FromCents(1);}}),
            serialize=Bench(21,()=>SaveCodec.Write(state))};
    }
    static void Main()
    {
        var output=new {baseCommit="51ce709",policy=FoodMenuPolicy.Wire,liveDefault=LiveDefault(),captured=new[]{Captured(4),Captured(8)},selection=new[]{Selection(4),Selection(8)},supply=Supply(),history=new[]{History(4),History(8)}};
        Console.WriteLine(JsonConvert.SerializeObject(new{checks,results=output},Formatting.Indented));
    }
}
