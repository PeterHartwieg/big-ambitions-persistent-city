using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

var root=Path.GetFullPath(args[0]);var prefix=args.Length>1?args[1]:"different-menus";
var rows=Enumerable.Range(1,2).SelectMany(day=>JObject.Parse(File.ReadAllText(Path.Combine(root,$"{prefix}-day-{day}-food-audit.json")))["decisions"]!.ToObject<List<FoodDecisionTrace>>()!).ToArray();
T Copy<T>(T value)=>JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value))!;
FoodIntent Choose(FoodDecisionTrace row,int? burgerPrice=null)
{
 var i=row.Intent;
 var intent=new FoodIntent { Id=i.Id,ConsumerId=i.ConsumerId,Day=i.Day,DesiredAt=i.DesiredAt,RequestedAt=i.RequestedAt,Deadline=i.Deadline,
  OriginPlaceId=i.OriginPlaceId,OriginActivity=i.OriginActivity,ContextEvidence=i.ContextEvidence,EatingMinutes=i.EatingMinutes,MaximumQueueMinutes=i.MaximumQueueMinutes,
  TimeCostMultiplier=i.TimeCostMultiplier,OutsideOption=i.OutsideOption,OutsideOptionScoreCents=i.OutsideOptionScoreCents,OutsideOptionEvidence=i.OutsideOptionEvidence };
 var businesses=Copy(row.Businesses);
 if(burgerPrice.HasValue)foreach(var b in businesses)foreach(var offer in b.Offers)if(offer.ProductId==FoodProducts.Burger)offer.PriceCents=burgerPrice.Value;
 // Only these reasons are actual early rejections. Missing-path trace messages
 // remain missing evidence and must not be promoted to proved impossibility.
 var known=new[]{"Closed or too late","Service unavailable","Out of stock","Over allowance","Beyond walking tolerance","Travel and service cannot fit break"};
 var refusals=row.RouteChecks.Where(p=>known.Contains(p.Value)).ToDictionary(p=>p.Key,p=>p.Value);
 FoodDemand.ChooseObserved(row.Consumer,businesses,row.Quotes,intent,intent.RequestedAt,refusals,row.MealKind);
 return intent;
}
foreach(var row in rows)
 if(!JToken.DeepEquals(JToken.FromObject(Choose(row)),JToken.FromObject(row.Intent)))throw new Exception("Baseline replay differs: "+row.Intent.Id);
var points=new List<object>();
foreach(int price in new[]{1250,1500,1875,2250})
{
 var pairs=rows.Select(r=>new{row=r,result=Choose(r,price)}).ToArray();
 var selected=pairs.Where(p=>p.result.Status==FoodIntentStatus.Travelling).ToArray();
 points.Add(new { burgerPriceCents=price,selectedOffers=selected.Length,
  byMealAndProduct=selected.GroupBy(p=>new{meal=p.row.MealKind.ToString(),product=p.result.ProductId}).Select(g=>new{g.Key.meal,g.Key.product,count=g.Count()}).ToArray(),
  originalBurgerChoices=pairs.Count(p=>p.row.Intent.ProductId==FoodProducts.Burger),
  burgerSwitches=pairs.Where(p=>p.row.Intent.ProductId==FoodProducts.Burger && p.result.ProductId!=FoodProducts.Burger).GroupBy(p=>p.result.ProductId.Length==0?p.result.Status.ToString():p.result.ProductId).ToDictionary(g=>g.Key,g=>g.Count()) });
}
var report=new{capture=prefix,replayed=rows.Length,exactBaselineReplay=true,points,
 boundary="Independent counterfactual offer decisions at recorded origins, routes, queues, stock, service and wallet. Prices only increase from the observed $12.50 baseline, so retained early affordability rejections remain valid. No native orders, new journeys, queue propagation, market-equilibrium sales or revenue are inferred; repeated choices can belong to the same meal. Recorded service availability is held fixed; no staffing event is introduced or removed."};
string output=JsonConvert.SerializeObject(report,Formatting.Indented);File.WriteAllText(Path.Combine(root,prefix=="different-menus"?"price-replay.json":"price-replay-"+prefix+".json"),output);Console.WriteLine(output);
