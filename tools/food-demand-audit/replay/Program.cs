using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

if(args[0]=="--scale") {
 double factor=args.Length>2?double.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture):1;
 string result=JsonConvert.SerializeObject(Scale.Run(factor),Formatting.Indented);File.WriteAllText(args[1],result);Console.WriteLine(result);return;
}
string file=Path.GetFullPath(args[0]), output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
var capture=JObject.Parse(File.ReadAllText(file));
var rows=capture["decisions"]!.ToObject<List<FoodDecisionTrace>>()!;
FoodIntent Fresh(FoodDecisionTrace row) {
 var i=row.Intent;
 return new FoodIntent { Id=i.Id,ConsumerId=i.ConsumerId,Day=i.Day,DesiredAt=i.DesiredAt,RequestedAt=i.RequestedAt,Deadline=i.Deadline,
 OriginPlaceId=i.OriginPlaceId,OriginActivity=i.OriginActivity,ContextEvidence=i.ContextEvidence,EatingMinutes=i.EatingMinutes,MaximumQueueMinutes=i.MaximumQueueMinutes,
 TimeCostMultiplier=i.TimeCostMultiplier,OutsideOption=i.OutsideOption,OutsideOptionScoreCents=i.OutsideOptionScoreCents,OutsideOptionEvidence=i.OutsideOptionEvidence };
}
FoodIntent Choose(FoodDecisionTrace row,bool old) {
 var intent=Fresh(row);
 if(old) {var state=FoodDemand.CreateObserved("audit","Retained native decision inputs",intent.Day,1,new[]{row.Consumer},row.Businesses,row.Quotes,new[]{intent});
 new FoodDemand(state).AdvanceTo(intent.RequestedAt,1);return state.Intents.Single();}
 FoodDemand.ChooseObserved(row.Consumer,row.Businesses,row.Quotes,intent,intent.RequestedAt);return intent;
}
foreach(var row in rows) {
 var actual=JsonConvert.SerializeObject(row.Intent);
 if(JsonConvert.SerializeObject(Choose(row,false))!=actual || JsonConvert.SerializeObject(Choose(row,true))!=actual)throw new Exception("Decision replay diverged: "+row.Intent.Id);
}
var points=new List<object>();
foreach(int price in new[]{300,500,700,900,1025,1200,1500,1800,2200,3000}) {
 var decisions=new List<FoodIntent>();
 foreach(var row in rows) {
 var businesses=JsonConvert.DeserializeObject<List<FoodBusiness>>(JsonConvert.SerializeObject(row.Businesses))!;
 foreach(var b in businesses)foreach(var offer in b.Offers)offer.PriceCents=price;
 var i=Fresh(row);FoodDemand.ChooseObserved(row.Consumer,businesses,row.Quotes,i,i.RequestedAt);decisions.Add(i);
 }
 points.Add(new {priceCents=price,chosen=decisions.Count(i=>i.Status==FoodIntentStatus.Travelling),declined=decisions.Count(i=>i.Status==FoodIntentStatus.DeclinedPurchase),unresolved=decisions.Count(i=>i.Status==FoodIntentStatus.Unresolved)});
}
var windows=new List<object>();
foreach(int minutes in new[]{45,60,75,90,120,180}) {
 int chosen=0,feasible=0;
 foreach(var row in rows) {
 var i=Fresh(row);i.Deadline=i.RequestedAt+minutes;
 FoodDemand.ChooseObserved(row.Consumer,row.Businesses,row.Quotes,i,i.RequestedAt);
 if(i.Choices.Any(c=>c.ScoreCents.HasValue))feasible++;
 if(i.Status==FoodIntentStatus.Travelling)chosen++;
 }
 windows.Add(new {minutes,feasible,chosen});
}
var benchmarks=new List<object>();
foreach(bool old in new[]{true,false}) {
 for(int n=0;n<300;n++)Choose(rows[n%rows.Count],old);
 foreach(int count in new[]{1000,2000,5000}) {
 var samples=new List<double>();var allocations=new List<long>();
 for(int trial=0;trial<7;trial++) {
 long before=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();
 for(int n=0;n<count;n++)Choose(rows[n%rows.Count],old);
 sw.Stop();samples.Add(sw.Elapsed.TotalMilliseconds);allocations.Add(GC.GetAllocatedBytesForCurrentThread()-before);
 }
 samples.Sort();allocations.Sort();benchmarks.Add(new {method=old?"one-person experiment":"direct shared choice",citizens=count,medianMilliseconds=samples[3],allocatedBytes=allocations[3]});
 }
}
var grouped=rows.GroupBy(r=>r.Intent.Choices.FirstOrDefault()?.Reason??"No advertised offer").ToDictionary(g=>g.Key,g=>g.Count());
var routeChecks=rows.SelectMany(r=>r.RouteChecks.Values).GroupBy(s=>s).ToDictionary(g=>g.Key,g=>g.Count());
var times=rows.Select(r=>r.ElapsedMilliseconds).OrderBy(x=>x).ToArray();
var report=new {nativeDecisions=rows.Count,exactReplay=true,choices=grouped,routeChecks,activities=rows.GroupBy(r=>r.Intent.OriginActivity.ToString()).ToDictionary(g=>g.Key,g=>g.Count()),
 feasible=rows.Count(r=>r.Intent.Choices.Any(c=>c.ScoreCents.HasValue)),bought=rows.Count(r=>r.Intent.Status==FoodIntentStatus.Travelling),
 nativeTiming=new {totalMilliseconds=times.Sum(),medianMilliseconds=times[times.Length/2],p95Milliseconds=times[(int)(times.Length*.95)],maxMilliseconds=times.Last(),routeCalls=rows.Sum(r=>r.RouteCalls)},
 priceSensitivity=points,timeWindowSensitivity=windows,benchmarks,
 note="Exact replay of captured native decisions. Sensitivities hold each person's observed origin, paths, stock and queue fixed; they do not produce new native sales or account for extra queue interactions. Longer windows ignore subsequent commitments and are diagnostic only. Benchmarks exclude native pathfinding, market reads and rendering."};
File.WriteAllText(Path.Combine(output,"report.json"),JsonConvert.SerializeObject(report,Formatting.Indented));Console.WriteLine(JsonConvert.SerializeObject(report,Formatting.Indented));
