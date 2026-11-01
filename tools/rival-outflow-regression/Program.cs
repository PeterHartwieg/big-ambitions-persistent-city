using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

static partial class Program
{
    static int checks;
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    sealed class Paths : IRoutePlanner, IFoodWalkingPace
    {
        public int Calls;
        public double WalkingMetresPerMinute => 120;
        public Route Plan(Place a, Place b, double at, TravelPreferences p)
        { Calls++; return new Route { Legs = new() { new RouteLeg { From=a.Position,To=b.Position,Minutes=1,Mode=TravelMode.Walk } } }; }
    }
    static CityBuildingController[] Fixtures(int count) => Enumerable.Range(0,count).Select(i=>new CityBuildingController {
        buildingRegistration=new() { StreetNumber=i,retailPrices=new() {new() {itemName=FoodProducts.Croissant,price=1}},
            scheduleDays=Enumerable.Range(1,7).Select(d=>new ScheduleDay {day=d,openingHourSlots=new(){new(){startingHour=0,endingHour=24}}}).ToList() }
    }).ToArray();
    static object Stats(double[] values,long bytes,int operations) => new { p50=values.Order().ElementAt(values.Length/2),
        p95=values.Order().ElementAt((int)Math.Ceiling(values.Length*.95)-1), max=values.Max(),
        spikesAtLeast30ms=values.Count(v=>v>=30), allocatedBytesPerOperation=(double)bytes/operations };
    static object Measure(int runs,Action action)
    {
        var times=new double[runs];long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<runs;i++) {long start=Stopwatch.GetTimestamp();action();times[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
        return Stats(times,GC.GetAllocatedBytesForCurrentThread()-bytes,runs);
    }
    static void Drain(NativeFoodMarket market,double at)
    { while(market.NeedsRivalRefresh(at)) market.ContinueRivalRefresh(at); }
    static void Advance(CitySimulator city,double at) {while(!city.AdvanceTo(at,1).CaughtUp) {}}

    static object Scale(int count)
    {
        SaveGameManager.Current=new();var fixtures=Fixtures(count);
        CityManager.Instance.cityBuildingControllers=fixtures.ToList();
        long heapBefore=GC.GetTotalMemory(true);
        var native=new NativeFoodMarket(fixtures);
        long before,refreshBytes=0;var refresh=new List<double>();
        while(native.NeedsRivalRefresh(1440)) {before=GC.GetAllocatedBytesForCurrentThread();long start=Stopwatch.GetTimestamp();native.ContinueRivalRefresh(1440);double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;refreshBytes+=GC.GetAllocatedBytesForCurrentThread()-before;refresh.Add(ms);}
        var coldRefresh=Stats(refresh.ToArray(),refreshBytes,refresh.Count);
        long revision=native.RivalRevision;
        refreshBytes=0;refresh.Clear();
        while(native.NeedsRivalRefresh(1500)) {before=GC.GetAllocatedBytesForCurrentThread();long start=Stopwatch.GetTimestamp();native.ContinueRivalRefresh(1500);double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;refreshBytes+=GC.GetAllocatedBytesForCurrentThread()-before;refresh.Add(ms);}
        var warmRefresh=Stats(refresh.ToArray(),refreshBytes,refresh.Count);
        long retainedReaderBytes=GC.GetTotalMemory(true)-heapBefore;
        Check(revision==native.RivalRevision,"unchanged refresh must preserve index revision");
        var metadata=native.RivalScopes.ToArray();
        var buildTimes=new List<double>(); before=GC.GetAllocatedBytesForCurrentThread();
        using var builder=FoodAwarenessCatalogue.BeginBuild(metadata,s=>new Position(10,0,0));
        long buildBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        bool complete;
        do {before=GC.GetAllocatedBytesForCurrentThread();long start=Stopwatch.GetTimestamp();complete=builder.Continue();double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;buildBytes+=GC.GetAllocatedBytesForCurrentThread()-before;buildTimes.Add(ms);Check(builder.LastWorkItems<=64,"bounded index work");} while(!complete);
        var indexBuild=Stats(buildTimes.ToArray(),buildBytes,buildTimes.Count);
        var index=builder.Result;
        var settings=new FoodAwarenessSettings {DiscoveryProbability=.2}; var traits=new ResidentTraits {Bands="2222222222"};
        int sampleDay=1,maxSources=0,maxShortlist=0;
        void Decision()
        {
            var image=index.Sample(77,"consumer",sampleDay++,new(0,0,0),new(0,0,0),new(0,0,0),settings,e=>true);
            var selected=index.Rank(image,new(0,0,0),traits,new(),new(77,"consumer",sampleDay,MealKind.Lunch,"stay",1),120);
            maxSources=Math.Max(maxSources,image.Union.Count);maxShortlist=Math.Max(maxShortlist,selected.Length);
        }
        var coldDecision=Measure(1,Decision); for(int i=0;i<50;i++) Decision();var warmDecision=Measure(1000,Decision);
        Check(maxSources<=24&&maxShortlist<=8,"32/8/16 envelope");
        var selectedId=metadata[0].BusinessId;
        for(int i=0;i<100;i++) native.ReadRival(selectedId);
        var oneRead=Measure(1000,()=>native.ReadRival(selectedId));
        var oldDoubleRead=Measure(1000,()=>{native.ReadRival(selectedId);native.ReadRival(selectedId);});
        Check(native.NativeWrites==0,"zero native checkout/writes");
        native.CloseRivals();
        return new {count,coldRefresh,warmRefresh,retainedReaderBytes,indexBuild,coldDecision,warmDecision,maxSources,maxShortlist,
            maximumLogicalRouteSlots=2*maxShortlist,arrivalBeforeTwoReads=oldDoubleRead,arrivalAfterOneRead=oneRead};
    }
    static CityState State(int consumers)
    {
        var people=Enumerable.Range(0,consumers).Select(i=>new CitizenIdentity {Id="r"+i,Name="Resident",Age=30,Persona=Persona.Foodie});
        return Population.Create(people,new[]{new Place {Id="home",Name="Home",Kind=PlaceKind.Home,Capacity=consumers},
            new Place {Id="address:Test%20Street:0:shop",Kind=PlaceKind.Shop,Name="Rival",Position=new(10,0,0)}},"d2","0.40.4",79,1440);
    }
    static object SaveSize(int consumers)
    {
        var state=State(consumers);state.Food=new();var city=new CitySimulator(state,new Paths());
        var native=new NativeFoodMarket(Fixtures(1));city.AttachFoodMarket(native);city.EnableHouseholdFinances();
        state.Schema=20;string baseline=PopulationSchema20.Write(state);state.Schema=21;
        string current=SaveCodec.Write(state);var counts=SaveCodec.CountRecords(state,current);
        state.Schema=20;var oldCounts=PopulationSchema20.CountRecords(state,baseline);state.Schema=21;
        var read=SaveCodec.Read(baseline);Check(read.Status==SaveReadStatus.Loaded,read.Error);
        return new {consumers,baselineBytes=System.Text.Encoding.UTF8.GetByteCount(baseline),currentBytes=System.Text.Encoding.UTF8.GetByteCount(current),
            deltaBytes=System.Text.Encoding.UTF8.GetByteCount(current)-System.Text.Encoding.UTF8.GetByteCount(baseline),
            streaming=counts.Streaming,semantic=counts.Semantic,baselineStreaming=oldCounts.Streaming,baselineSemantic=oldCounts.Semantic};
    }
    static void NativeProof()
    {
        SaveGameManager.Current=new();var fixtures=Fixtures(1);CityManager.Instance.cityBuildingControllers=fixtures.ToList();
        var state=State(1);state.Citizens[0].NextEventMinute=state.Citizens[0].ActivityUntilMinute=2870;
        state.Food=new();var paths=new Paths();var market=new NativeFoodMarket(fixtures);var city=new CitySimulator(state,paths);city.AttachFoodMarket(market);
        state.Food.ChoiceStartsDay=state.Food.AwarenessStartsDay=state.Food.RivalStartsDay=1;
        string nativeBefore=JsonConvert.SerializeObject(fixtures);
        var meal=state.Food.Residents.Single();city.InspectFoodAwareness(meal.ResidentId);
        // A fresh metadata interval may precede capture at this later logical minute.
        for(int units=0;units<512&&meal.Decision==null;units++) city.AdvanceTo(meal.DesiredAt,1);
        Check(meal.Decision?.Shops.Count==1,"captured native rival");
        // Resume the held candidate from a genuinely cold reader while new rival choices are disabled.
        string held=SaveCodec.Write(city.State);var restored=SaveCodec.Read(held);Check(restored.Status==SaveReadStatus.Loaded,restored.Error);
        market.CloseRivals();market=new NativeFoodMarket(fixtures);city=new CitySimulator(restored.State!,paths);
        city.AttachFoodMarket(market,rivalSettings:new RivalFoodSettings {RivalChoiceWeight=0,RivalAssumedServiceMinutes=30});
        Advance(city,meal.DesiredAt);meal=city.State.Food!.Residents.Single();Check(meal.Status==MealStatus.Travelling&&meal.ServiceMinutes==5,"native held rival selected with frozen estimate");
        for(int i=0;i<4;i++)
        {
            string checkpoint=SaveCodec.Write(city.State);var loaded=SaveCodec.Read(checkpoint);Check(loaded.Status==SaveReadStatus.Loaded,loaded.Error);
            market.CloseRivals();market=new NativeFoodMarket(fixtures);city=new CitySimulator(loaded.State!,paths);
            city.AttachFoodMarket(market,rivalSettings:new RivalFoodSettings {RivalChoiceWeight=0});
            Advance(city,city.State.Minute+2);
        }
        meal=city.State.Food!.Residents.Single();Check(meal.Purchases==1&&meal.LegacyExternalFoodSpent==Money.FromCents(100),"exactly once native-reader backed outflow after weight-zero cold reload");
        Advance(city,city.State.Minute+20);Check(city.State.Citizens[0].MealOuting==null,"native reader timed return");
        Check(nativeBefore==JsonConvert.SerializeObject(fixtures),"all native fixture fields unchanged");Check(market.NativeWrites==0,"no native write call");
        market.CloseRivals();
    }
    static object Settlements()
    {
        string target="10"+RivalFoodTerms.Fingerprint(new FoodAlternativeObservation("rival","shop","street",1,0,"food","rival",FoodAlternativeKind.Rival,true,true,false,null,0,
            new[]{new FoodAdvertisedOffer(FoodProducts.Croissant,Money.FromCents(100))},new[]{new FoodAdvertisedHours(0,0,1440)}));
        var meals=Enumerable.Range(0,10000).Select(_=>new ResidentMeal {External=target,Status=MealStatus.Serving,Price=Money.FromCents(100),Budget=new ResidentBudget {
            OpeningBalance=Money.FromCents(1000),Balance=Money.FromCents(1000),DailyHomeFoodCents=100}}).ToArray();int cursor=0;
        var timing=Measure(meals.Length,()=>Check(ExternalFoodSettlement.TryPay(meals[cursor++]),"ledger payment"));
        Check(meals.All(m=>HouseholdFinance.CashConserved(m.Budget!)),"extended conservation");
        return timing;
    }
    static object UnknownStockReservationKeys()
    {
        var scopes=Fixtures(8).Select(c=>new {Place="address:Test%20Street:"+c.buildingRegistration.StreetNumber+":shop",Product=FoodProducts.Croissant}).ToArray();
        var reservations=new Dictionary<string,int>();int checksum=0;
        void Before() {foreach(var s in scopes) {reservations.TryGetValue(s.Place+"\n"+s.Product,out int held);checksum+=held;}}
        void After() {foreach(var s in scopes) {int? units=null;int held=0;if(units.HasValue) reservations.TryGetValue(s.Place+"\n"+s.Product,out held);checksum+=held;}}
        for(int i=0;i<100;i++) {Before();After();}
        var before=Measure(1000,Before);var after=Measure(1000,After);Check(checksum==0,"unknown stock never changes reservations");
        return new {eightUnknownStockOffersBefore=before,eightUnknownStockOffersAfter=after};
    }
    static void OverflowBeforeCommit()
    {
        SaveGameManager.Current=new();var fixtures=Fixtures(1);var state=State(1);
        state.Citizens[0].NextEventMinute=state.Citizens[0].ActivityUntilMinute=2870;
        state.Food=new();var market=new NativeFoodMarket(fixtures);var city=new CitySimulator(state,new Paths());city.AttachFoodMarket(market);
        state.Food.ChoiceStartsDay=state.Food.AwarenessStartsDay=state.Food.RivalStartsDay=1;
        var meal=state.Food.Residents.Single();meal.Spent=Money.FromUnits(long.MaxValue);
        Advance(city,meal.DesiredAt);
        Check(meal.Status==MealStatus.Missed&&state.Citizens[0].MealOuting==null,"overflow refused before physical commitment");
        Check(meal.Purchases==0&&meal.LegacyExternalFoodSpent==Money.Zero&&market.NativeWrites==0,"overflow leaves money/native state unchanged");
        market.CloseRivals();
    }
    static object WholeDay(int alternatives)
    {
        SaveGameManager.Current=new();var fixtures=Fixtures(alternatives);
        CityManager.Instance.cityBuildingControllers=fixtures.ToList();
        var state=State(957);
        for(int i=1;i<alternatives;i++) state.Places.Add(new Place {Id="address:Test%20Street:"+i+":shop",Kind=PlaceKind.Shop,Name="Rival",Position=new(10,0,0)});
        foreach(var person in state.Citizens) person.NextEventMinute=person.ActivityUntilMinute=2870;
        state.Food=new FoodCommerceState {MealRhythmVersion=MealRhythm.PolicyVersion,RepresentationFactor=2,HouseholdFinanceVersion=HouseholdFinance.PolicyVersion};
        var paths=new Paths();var market=new NativeFoodMarket(fixtures);var city=new CitySimulator(state,paths);city.AttachFoodMarket(market);
        state.Food.ChoiceStartsDay=state.Food.AwarenessStartsDay=state.Food.RivalStartsDay=1;
        var windows=new List<double>(16384);long units=0,continuations=0,bytes=GC.GetAllocatedBytesForCurrentThread();long begin=Stopwatch.GetTimestamp();
        AdvanceResult step;
        do {long start=Stopwatch.GetTimestamp();step=city.AdvanceTo(2869,128);windows.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);units+=step.Events;continuations+=step.FoodContinuationEvents;} while(!step.CaughtUp);
        double milliseconds=Stopwatch.GetElapsedTime(begin).TotalMilliseconds;bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Check(state.Food.Residents.All(m=>HouseholdFinance.CashConserved(m.Budget!)),"whole-day conservation");
        Check(state.Food.Residents.All(m=>m.Excursion==null)&&state.Citizens.All(c=>c.MealOuting==null),"whole-day returns completed");
        Check(market.NativeWrites==0,"whole-day zero native rival writes");
        var result=new {alternatives,consumers=state.Food.Residents.Count,milliseconds,allocatedBytes=bytes,workUnits=units,continuations,
            advance128UnitSamples=Stats(windows.ToArray(),bytes,windows.Count),routes=paths.Calls,market.RivalRefreshUnits,market.RivalDetailReads,
            purchases=state.Food.Residents.Sum(m=>m.Purchases),pendingMeals=state.Food.Residents.Count(m=>m.Status==MealStatus.Travelling||m.Status==MealStatus.Serving||m.Decision!=null),
            caughtUp=step.CaughtUp,cityMinute=state.Minute,diagnostic=city.DumpRivalFood()};
        market.CloseRivals();return result;
    }
    static void Main(string[] args)
    {
        if(args.Length == 1 && args[0].StartsWith("review-", StringComparison.Ordinal))
        { ReviewCase(args[0]); Console.WriteLine("PASS " + args[0] + ": " + checks + " checks"); return; }
        ReviewCase("review-non-food"); ReviewCase("review-refresh-failure"); ReviewCase("review-saved-failure");
        OverflowBeforeCommit();NativeProof();var scales=new[]{Scale(1024),Scale(20000)};
        var wholeDay=new[]{WholeDay(1024),WholeDay(20000)};
        Console.WriteLine(JsonConvert.SerializeObject(new {scales,settlement=Settlements(),unknownStockReservations=UnknownStockReservationKeys(),saves=new[]{SaveSize(1914),SaveSize(5000)},wholeDay,checks},Formatting.Indented));
    }
}
