using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buildings.Retail.Simulation;
using Helpers;
using PersistentCity.Core;
using PersistentCity.Runtime;

static partial class Program
{
    static bool BeforeSupported(BuildingRegistration r) => r.RentedByPlayer && r.HasEstablishedBusiness && !r.AvailableForRent
        && r.cachedAvailableProducts.Count >= 1 && r.cachedAvailableProducts.Count <= FoodMenuPolicy.LiveMaximumOffers
        && r.cachedAvailableProducts.All(FoodProducts.Known)
        && r.cachedAvailableProducts.Distinct(StringComparer.Ordinal).Count()==r.cachedAvailableProducts.Count
        && BusinessTypeHelper.GetData(r.businessTypeName).simulator.GetType()==typeof(FullServiceBusinessSimulator)
        && !BusinessTypeHelper.GetData(r.businessTypeName).hasEntranceFee
        && BusinessTypeHelper.GetData(r.businessTypeName).HasTag(BigAmbitions.Tags.TagRef.Businesstag.customersneedpaperbags);
    static void CachedBagPolicyChecks()
    {
        foreach(string missing in new[]{"foodtrucks:itemname_dumplings","foodtrucks:itemname_choppedcheese","other:historical\\name"})
        {
            Hooks.Reset(); SaveGameManager.Current.modData.Clear();
            var reg=Registration(1,5,7,3);reg.cachedAvailableProducts.Clear();reg.cachedAvailableProducts.Add(missing);
            var controller=new CityBuildingController {buildingRegistration=reg};CityManager.Instance.cityBuildingControllers=new[]{controller};
            string place=NativeFoodMarket.PlaceId(reg);
            var state=new FoodCommerceState {Shops={new FoodScope {PlaceId=place,BusinessId="retained-business",ProductId=missing}}};
            using var market=new NativeFoodMarket(state,new[]{new Place {Id=place,Kind=PlaceKind.Shop}});
            Check(state.Shops.Single().ProductId==missing,"missing canonical scope loads without remapping");
            Check(!market.CanServe("retained-business") && market.NativePurchases==0 && reg.unprocessedCompletedOrders.Count==0,
                "missing definition never grants checkout authority");
            Check(market.ScopeStatus(place).Contains("incomplete"),"dynamic refusal names remaining contract");
        }
        using(var f=new Fixture())
        {
            var type=BusinessTypeHelper.GetData(f.Reg.businessTypeName);
            Check(BeforeSupported(f.Reg)==NativeFoodMarket.Supported(f.Reg),"original authored/native bag policy agrees");
            long calls=type.TagCalls;
            for(int i=0;i<10000;i++) Check(NativeFoodMarket.Supported(f.Reg),"clean cached authored policy");
            Check(type.TagCalls==calls,"Supported never calls HasTag");
            type.NativeBags=false;
            Check(f.Market.CaptureBusiness(f.BusinessId,Minute)==null,"authored/native disagreement refuses capture");
            Check(!f.Market.CanServe(f.BusinessId),"bag policy mismatch withdraws whole business");
        }
        using(var f=new Fixture(authoredBags:false))
        {
            Check(f.State.Shops.Count==0 && !NativeFoodMarket.Supported(f.Reg),"native cache cannot grant absent authored bag policy");
        }
        using(var f=new Fixture())
        {
            var meal=f.Meal("late-bag-policy");
            Hooks.OpenCheck=()=>BusinessTypeHelper.GetData(f.Reg.businessTypeName).NativeBags=false;
            Check(!f.Market.Buy(meal,Minute).Paid && f.Bags==3 && f.Reg.unprocessedCompletedOrders.Count==0,
                "late native bag disagreement refuses before receipt and order");
        }
        using(var f=new Fixture())
        {
            var meal=f.Meal("late-authored-policy");
            Hooks.OpenCheck=()=>BusinessTypeHelper.GetData(f.Reg.businessTypeName).AuthoredTags.Clear();
            Check(!f.Market.Buy(meal,Minute).Paid && f.Bags==3 && f.Reg.unprocessedCompletedOrders.Count==0,
                "late authored tag mutation refuses despite stale native tag cache");
        }
        using(var f=new Fixture())
        {
            foreach(string name in new[]{FoodProducts.Croissant,FoodProducts.Burger,FoodProducts.HotDog})
            { f.Reg.cachedAvailableProducts.Clear();f.Reg.cachedAvailableProducts.Add(name);Check(BeforeSupported(f.Reg)==NativeFoodMarket.Supported(f.Reg),"all original three retain authored policy"); }
            f.Reg.cachedAvailableProducts.Add(FoodProducts.HotDog);
            Check(!NativeFoodMarket.Supported(f.Reg),"duplicate detection remains ordinal");
            f.Reg.cachedAvailableProducts.RemoveAt(1);
            var before=MeasureSupported(()=>BeforeSupported(f.Reg));
            var after=MeasureSupported(()=>NativeFoodMarket.Supported(f.Reg));
            Console.WriteLine("E5b authored/duplicate discovery before="+before+" after="+after+"; conditional native doubles, not mod-work/frame gate");
        }
    }
    static string MeasureSupported(Func<bool> call)
    {
        for(int i=0;i<1000;i++) if(!call()) throw new Exception("perf fixture");
        const int count=10000; var times=new double[count];long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<count;i++) {long start=Stopwatch.GetTimestamp();if(!call())throw new Exception("perf fixture");times[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
        bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;Array.Sort(times);
        return System.Text.Json.JsonSerializer.Serialize(new {bytesPerCall=bytes/(double)count,p50=times[count/2],p95=times[(int)(count*.95)],max=times[^1],spikes30=times.Count(t=>t>=30)});
    }
}
