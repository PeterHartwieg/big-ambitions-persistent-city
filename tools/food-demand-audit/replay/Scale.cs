using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Activity = PersistentCity.Core.Activity;
using Newtonsoft.Json;
using PersistentCity.Core;

internal static class Scale
{
    internal static object Run(double clockFactor = 1)
    {
        if(!double.IsFinite(clockFactor) || clockFactor<.1 || clockFactor>1)throw new ArgumentOutOfRangeException(nameof(clockFactor));
        var reports=new List<object>();
        var expected=new Dictionary<int,(long events,int sales,string orders)>();
        foreach(int population in new[]{1000,2000,5000})
        foreach(bool bounded in new[]{false,true})
        {
            var times=new List<double>();var bytes=new List<long>();long events=0;int calls=0,reads=0,sales=0;double maxEvent=0;string orderHash="";
            for(int trial=0;trial<4;trial++)
            {
                var places=new List<Place>();
                for(int j=0;j<100;j++)places.Add(new Place {Id="h"+j,Name="Home "+j,Kind=PlaceKind.Home,Capacity=population,Position=new Position(j%10*30,0,j/10*30)});
                for(int j=0;j<8;j++)places.Add(new Place {Id="s"+j,Name="Shop "+j,Kind=PlaceKind.Shop,Position=new Position(j%4*80+3,0,j/4*160+3)});
                var state=Population.Create(Enumerable.Range(0,population).Select(n=>new CitizenIdentity {Id="r"+n,Name="Person "+n,Age=30,Persona=(Persona)(n%8)}),places,"scale","0.41.1",19,1440);
                for(int j=0;j<population;j++) {
                    var c=state.Citizens[j];c.HomeId=c.PlaceId="h"+(j%100);c.Phase=CitizenPhase.AtPlace;c.Activity=Activity.Home;
                    c.NextEventMinute=c.ActivityUntilMinute=2879;c.NextDepartureMinute=c.NextJobChangeMinute=ResidentLifecycle.Never;
                    c.Preferences.WalkingToleranceMetres=1000;
                }
                state.Food=new FoodCommerceState {Shops=Enumerable.Range(0,8).Select(n=>new FoodScope {PlaceId="s"+n,BusinessId="b"+n,ProductId="pastry"}).ToList()};
                var market=new Market();var routes=new Walking {Bounded=bounded,ClockFactor=clockFactor};var city=new CitySimulator(state,routes);city.AttachFoodMarket(market);
                long prior=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();long count=0;double peak=0;
                while(true) {
                    long t=Stopwatch.GetTimestamp();var result=city.AdvanceFrameSlice(2870,1);peak=Math.Max(peak,Stopwatch.GetElapsedTime(t).TotalMilliseconds);count+=result.Events;
                    if(result.CaughtUp)break;
                }
                sw.Stop();if(trial>0) {times.Add(sw.Elapsed.TotalMilliseconds);bytes.Add(GC.GetAllocatedBytesForCurrentThread()-prior);}
                if(trial==3){events=count;calls=routes.Calls;reads=market.Reads;sales=market.Orders.Count;maxEvent=peak;orderHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",market.Orders))));}
                if(state.Food.Residents.Sum(m=>m.Purchases)!=market.Orders.Count || market.Orders.Count!=market.Orders.Distinct().Count())throw new Exception("Scale conservation failure");
            }
            if(!bounded)expected.Add(population,(events,sales,orderHash));
            else if(expected[population]!=(events,sales,orderHash))throw new Exception("Pruning changed purchase identity/order or event count");
            times.Sort();bytes.Sort();reports.Add(new {orderHash,population,shops=8,geometricPruning=bounded,totalEvents=events,routeCalls=calls,marketReads=reads,sales,
                medianMilliseconds=times[times.Count/2],allocatedBytes=bytes[bytes.Count/2],maxEventMilliseconds=maxEvent});
        }
        return new {clockFactor,cases=reports,note="Full Core scheduler with eight competing stocked shops and 100 distributed home origins. One-event frame slices. Synthetic complete walking paths and in-memory market; native pathfinding, physics, UI and rendering excluded. No per-frame scan of all citizens."};
    }
    private sealed class Walking : IRoutePlanner,IWalkingTimeBound
    {
        public bool Bounded;public int Calls;public double ClockFactor=1;
        public double MinimumWalkingMinutes(Place from,Place to)=>Bounded?Math.Max(0,Position.Distance(from.Position,to.Position)-.1)/2*ClockFactor:0;
        public Route? Plan(Place from,Place to,double at,TravelPreferences preferences) {
            Calls++;double d=Position.Distance(from.Position,to.Position);if(d>preferences.WalkingToleranceMetres)return null;
            return new Route {Legs=new() {new RouteLeg {From=from.Position,To=to.Position,Minutes=Math.Max(.01,d/2*ClockFactor),Mode=TravelMode.Walk}}};
        }
    }
    private sealed class Market : IFoodMarket
    {
        public int Reads;public List<string> Orders=new();private readonly int[] units=Enumerable.Repeat(10000,8).ToArray();
        public double EarliestMinute=>0;
        public IReadOnlyList<FoodBusiness> Read(double minute) {
            Reads++;return Enumerable.Range(0,8).Select(j=>new FoodBusiness {Id="b"+j,PlaceId="s"+j,Name="Shop "+j,MealsPerHour=15,
                Hours=Enumerable.Range(0,7).Select(d=>new OpeningWindow {Day=d,FromMinute=480,ToMinute=1080}).ToList(),
                Offers=new() {new FoodOffer {ProductId="pastry",PriceCents=700,Units=units[j]}}}).ToArray();
        }
        public FoodSale Buy(ResidentMeal meal,double minute) {int n=int.Parse(meal.BusinessId.Substring(1));if(units[n]==0)return new(false,"Sold out");units[n]--;Orders.Add(meal.IntentId);return new(true,"Synthetic receipt");}
    }
}
