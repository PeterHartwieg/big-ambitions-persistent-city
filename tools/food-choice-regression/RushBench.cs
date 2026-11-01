using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Persistence;
using PersistentCity.Core;
using Activity = PersistentCity.Core.Activity;

static partial class Program
{
    static object RushBench()
    {
        var results=new List<object>();
        var digests=new Dictionary<string,string>();
        for(int sample=-1;sample<3;sample++)
        foreach(string mode in new[]{"all_feasible","closed","refused_outward","existing_shop"})
        foreach(bool separateContinuations in sample%2==0 ? new[]{false,true} : new[]{true,false})
        {
            const int residents=957;int factor=mode=="existing_shop" ? 1 : CityBalance.ConsumerRepresentationFactor;
            var places=new List<Place> {new() {Id="home",Name="Home",Kind=PlaceKind.Home,Capacity=residents}};
            places.AddRange(Enumerable.Range(0,16).Select(i=>new Place {Id="s"+i,Name="Shop",Kind=PlaceKind.Shop,Position=new(1+i,0,0)}));
            var state=Population.Create(Enumerable.Range(0,residents).Select(i=>new CitizenIdentity {Id="r"+i,Name="Resident",Age=30,Persona=Persona.Foodie}),places,"rush","0.40.4",31,1440);
            foreach(var p in state.Citizens)
            {p.NextEventMinute=p.ActivityUntilMinute=2870;if(mode=="existing_shop") {p.PlaceId="s0";p.Activity=Activity.Shop;}}
            state.Food=new FoodCommerceState {MealRhythmVersion=1,RepresentationFactor=factor,
                Shops=places.Skip(1).Select((p,i)=>new FoodScope {PlaceId=p.Id,BusinessId="b"+i,ProductId=FoodProducts.Croissant}).ToList()};
            var market=new Market();
            foreach(var s in state.Food.Shops) market.Shops.Add(s.BusinessId,new FoodBusiness {Id=s.BusinessId,PlaceId=s.PlaceId,MealsPerHour=6000,
                Hours=mode=="closed" ? new() : Enumerable.Range(0,7).Select(d=>new OpeningWindow {Day=d,FromMinute=0,ToMinute=1440}).ToList(),
                Offers=new(){new() {ProductId=FoodProducts.Croissant,Price=Money.FromCents(100),Units=100000}}});
            var paths=new Paths {Refuse=mode=="refused_outward"};var city=new CitySimulator(state,paths);city.AttachFoodMarket(market);
            state.Minute=2160;state.Food.ChoiceStartsDay=1;
            foreach(var m in state.Food.Residents) {m.Kind=MealKind.Lunch;m.DesiredAt=m.DueAt=2160;m.ExpiresAt=2205;}
            city=new CitySimulator(state,paths);city.AttachFoodMarket(market);
            int completed=0,frames=0,events=0,citizenEvents=0,continuationEvents=0;city.FoodDecisionObserved+=_=>completed++;
            var backlog=new List<int>();int consumers=state.Food.Residents.Count;
            var whole=Stopwatch.StartNew();
            while(!city.IsCaughtUpTo(2160))
            {
                long start=Stopwatch.GetTimestamp();int units=0,citizens=0;
                do
                {
                    var r=city.AdvanceFrameSlice(2160,1);events+=r.Events;units+=r.Events;
                    if(r.PlanningPending) throw new Exception("Stub routes must not leave native planning pending");
                    citizenEvents+=r.CitizenEvents;continuationEvents+=r.FoodContinuationEvents;
                    citizens+=separateContinuations ? r.CitizenEvents : r.Events-r.TrafficEvents;
                    if(r.CaughtUp) break;
                } while(units<128 && citizens<16 && Stopwatch.GetElapsedTime(start).TotalMilliseconds<2);
                city.SynchronizeSnapshot();
                frames++;backlog.Add(consumers-completed);
            }
            whole.Stop();
            if(completed!=consumers) throw new Exception("Rush did not drain every decision");
            var windows=new[]{1.0/3,1.0,3.0}.Select(rate=>
            {
                int available=(int)Math.Floor(45/rate*100);
                return new {gameMinutesPerRealSecond=rate,windowFrames=available,pendingAtWindowEnd=available>=backlog.Count ? 0 : backlog[available-1]};
            }).ToArray();
            string digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SaveCodec.Write(city.State))));
            if(digests.TryGetValue(mode,out string? expected) && digest!=expected) throw new Exception("Frame accounting changed saved choice/reason/money state");
            digests[mode]=digest;
            if(citizenEvents!=consumers || continuationEvents!=events-consumers) throw new Exception("Each decision must count once as a citizen event");
            if(sample<0) continue; // Warm both accounting modes before alternating measured runs.
            results.Add(new {mode,sample,frameAccounting=separateContinuations ? "separate_continuations" : "R1_all_units_are_citizens",
                residents,factor,consumers,events,citizenEvents,continuationEvents,eventsPerDecision=events/(double)consumers,
                eventCapFrames=separateContinuations ? Math.Max((int)Math.Ceiling(events/128.0),(int)Math.Ceiling(citizenEvents/16.0)) : (int)Math.Ceiling(events/16.0),
                measuredFramesWith16Citizen128Unit2MsLoop=frames,estimatedSecondsAt100Fps=frames/100.0,offlineCpuMs=whole.Elapsed.TotalMilliseconds,
                logicalRouteRequests=paths.Calls,lunchWindowMinutes=45,backlog=windows,savedStateSha256=digest});
        }
        return new {boundary="Synthetic simultaneous lunch; stub paths; one warmup + three alternating samples per mode/accounting, 16 citizen / 128 total units / 2 ms soft budget. Snapshot published per frame. Frame/backlog estimates at 100 fps, explicit clock-rate scenarios. Not native throughput or path cost.",results};
    }
}
