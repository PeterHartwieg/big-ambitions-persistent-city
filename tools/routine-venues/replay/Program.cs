using System.Diagnostics;
using Activity = PersistentCity.Core.Activity;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

var reports = new List<object>();
foreach (int count in new[] { 1000, 2000, 5000 })
{
    string? expected = null;
    foreach (int budget in new[] { 1, 128 })
    {
        var places = Enumerable.Range(0, 100).Select(i => new Place { Id="h"+i, Name="Home", District="a", Kind=PlaceKind.Home,
            Capacity=count, Position=new Position(i%10*50,0,i/10*50) }).ToList();
        for(int i=0;i<400;i++) places.Add(new Place { Id="v"+i, Name="Venue", District="a",
            Kind=i%2==0?PlaceKind.Shop:PlaceKind.Leisure, Position=new Position(i%20*35+5,0,i/20*35+5),
            OpenMinute=i%4==0?1080:480, CloseMinute=i%5==0?1080:1380 });
        var city=Population.Create(Enumerable.Range(0,count).Select(i=>new CitizenIdentity { Id="c"+i, Name="Citizen", District="a", Age=30, Persona=(Persona)(i%8) }),
            places,"optional-scale","0.41.1",19,1440);
        foreach(var c in city.Citizens) {c.Preferences.AllowTransit=false;c.Preferences.OwnsCar=false;}
        var planner=new Walking();var sim=new CitySimulator(city,planner);
        var outcomes=new Dictionary<string,int>();int decisions=0;long events=0;double maximum=0;
        sim.RoutineObserved+=d=>{string key=d.Outcome+":"+d.Reason;outcomes[key]=outcomes.GetValueOrDefault(key)+1;
            if(d.Outcome=="selected" || (d.Outcome=="missed" && d.Reason.Contains("optional"))) decisions++;};
        long allocated=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
        while(true) {long t=Stopwatch.GetTimestamp();var advance=sim.AdvanceTo(2880,budget);maximum=Math.Max(maximum,Stopwatch.GetElapsedTime(t).TotalMilliseconds);events+=advance.Events;if(advance.CaughtUp)break;}
        timer.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SaveCodec.Write(city))));
        if(expected!=null && hash!=expected)throw new Exception("Event cadence changed residents' outcomes");expected=hash;
        reports.Add(new {population=count,eventBudget=budget,events,planner.Calls,decisions,milliseconds=timer.Elapsed.TotalMilliseconds,allocatedBytes=allocated,maxSliceMs=maximum,
            visits=city.Citizens.Sum(c=>c.RecentVisits.Count(v=>v.Activity is Activity.Shop or Activity.Leisure)),missed=city.Citizens.Sum(c=>c.MissedActivities),outcomes,hash});
    }
}
File.WriteAllText(args[0],JsonConvert.SerializeObject(new {reports,note="Synthetic one-third-speed walking, 400 mixed-hours venues, no work assignments. Full optional routine scheduler; native navigation, physical bodies and rendering excluded. Snapshot hash must match at event budgets 1 and 128."},Formatting.Indented));
Console.WriteLine("All three population sizes have identical saved outcomes at both event budgets.");
sealed class Walking:IRoutePlanner,IWalkingTimeBound
{
    public int Calls;
    public double MinimumWalkingMinutes(Place a,Place b)=>Position.Distance(a.Position,b.Position)/6;
    public Route? Plan(Place a,Place b,double at,TravelPreferences prefs)
    {Calls++;if(b.Id.StartsWith("v") && int.Parse(b.Id[1..])%13==0)return null;
     double d=Position.Distance(a.Position,b.Position);if(d>prefs.WalkingToleranceMetres)return null;
     return new Route {Legs=new(){new RouteLeg {From=a.Position,To=b.Position,Mode=TravelMode.Walk,Minutes=Math.Max(.01,d/6)}}};}
}
