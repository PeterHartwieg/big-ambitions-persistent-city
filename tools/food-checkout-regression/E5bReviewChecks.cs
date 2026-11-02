using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;

static partial class Program
{
    static void ReviewStatusScanMeasurement()
    {
        var products=new List<string> {Croissant,Burger,FoodProducts.HotDog};
        bool IndexedKnown()
        {for(int i=0;i<products.Count;i++) if(!FoodProducts.Known(products[i])) return false;return true;}
        Console.WriteLine("Review MenuStatus scan fragment before="+MeasureSupported(()=>!products.Any(p=>!FoodProducts.Known(p)))
            +" after="+MeasureSupported(IndexedKnown)+"; test CLR only, not native MenuStatus/frame timing");
    }
    static void ReviewIsolationChecks()
    {
        foreach (string fault in new[] { "price", "unsupported", "width", "non-food", "historical", "generation" })
        foreach (bool batchFirst in new[] { true, false })
        foreach (bool reverseControllers in new[] {true,false})
        {
            Hooks.Reset(); SaveGameManager.Current.modData.Clear(); SaveGameManager.Current.Minute=0;
            var controllers=Enumerable.Range(1,2).Select(i=>new CityBuildingController {buildingRegistration=Registration(i,5,7,3)}).ToArray();
            CityManager.Instance.cityBuildingControllers=reverseControllers ? controllers.Reverse().ToArray() : controllers;
            var state=new FoodCommerceState();
            using var market=new NativeFoodMarket(state,controllers.Select(c=>new Place {Id=NativeFoodMarket.PlaceId(c.buildingRegistration!),Kind=PlaceKind.Shop}).ToArray());
            var bad=controllers[0].buildingRegistration!;var good=controllers[1].buildingRegistration!;
            var badScope=state.Shops.Single(s=>s.PlaceId==NativeFoodMarket.PlaceId(bad));
            var goodScope=state.Shops.Single(s=>s.PlaceId==NativeFoodMarket.PlaceId(good));
            Check(market.Read(Minute).Count==2,"prime both healthy shops in the same observation minute");
            if(fault=="price") bad.retailPrices.RemoveAll(p=>p.itemName==Burger);
            if(fault=="unsupported") bad.cachedAvailableProducts.Add("mod:drink");
            if(fault=="width") bad.cachedAvailableProducts.AddRange(new[]{FoodProducts.HotDog,Croissant,Burger});
            if(fault=="non-food") {bad.businessTypeName="other";Helpers.BusinessTypeHelper.GetData("other").simulator=new object();}
            if(fault=="historical") {bad.RentedByPlayer=false;badScope.ProductId="missing:historic";}
            if(fault=="generation") badScope.BusinessId="old-generation";
            if(!batchFirst) Check(market.CaptureBusiness(goodScope.BusinessId,Minute)!=null,"healthy individual capture before batch");
            var businesses=market.Read(Minute);
            Check(businesses.Count==1 && businesses[0].Id==goodScope.BusinessId,"bad shop must not remove healthy batched result: "+fault);
            Check(market.CanServe(goodScope.BusinessId),"unrelated ownership survives batch refusal: "+fault);
            market.RefreshKnownOwnership();
            Check(market.CaptureBusiness(goodScope.BusinessId,Minute)!=null,"healthy result survives ownership refresh: "+fault);
            var meal=new ResidentMeal {ResidentId="healthy-"+fault,Day=SaveGameManager.Current.Day,Kind=MealKind.Lunch,
                PlaceId=goodScope.PlaceId,BusinessId=goodScope.BusinessId,ProductId=Croissant,Price=Money.FromCents(300),Allowance=Money.FromCents(1200),ServiceMinutes=10};
            Check(market.Buy(meal,Minute).Paid && good.unprocessedCompletedOrders.Count==1 && bad.unprocessedCompletedOrders.Count==0,
                "only the healthy business books its one native order: "+fault);
        }
    }

    static void ReviewRecoveryChecks()
    {
        foreach(string fault in new[]{"price","unknown","native-bag","empty-shelf"})
        {
        using var f=new Fixture();var price=f.Reg.retailPrices.Single(p=>p.itemName==Burger);
        var cargo=f.Reg.Shelves[0].cargoInstances!.ToList();
        if(fault=="price") f.Reg.retailPrices.Remove(price);
        if(fault=="unknown") f.Reg.Shelves[0].cargoInstances!.Add(new BigAmbitions.Items.Cargo {itemName=Croissant,amount=1});
        if(fault=="native-bag") Helpers.BusinessTypeHelper.GetData(f.Reg.businessTypeName).NativeBags=false;
        if(fault=="empty-shelf") {Hooks.KeepEmptyProducer=true;f.Reg.Shelves[0].cargoInstances!.Clear();}
        Check(f.Market.CaptureBusiness(f.BusinessId,Minute)==null,"unverified contract suspends service: "+fault);
        var row=NativeFoodOwnership.Find(f.Reg)!;long hour=SaveGameManager.Current.Day*24L+SaveGameManager.Current.Hour;
        if(fault=="price") f.Reg.retailPrices.Add(price);
        f.Reg.Shelves[0].cargoInstances=cargo;Hooks.KeepEmptyProducer=false;
        Helpers.BusinessTypeHelper.GetData(f.Reg.businessTypeName).NativeBags=true;
        f.Market.RefreshKnownOwnership();
        Check(!f.Market.CanServe(f.BusinessId),"repair cannot resume in the refused hour");
        int originalHour=SaveGameManager.Current.Hour;
        try
        {
            SaveGameManager.Current.Hour++;
            f.Market.RefreshKnownOwnership();
            Check(row.OwnsHour(hour+1),"ending work stays protected before drain");
            Check(!row.Boundary(hour+1,false,false,true),"queued work prevents handoff");
            Check(row.Boundary(hour+1,true,false,true),"drained work transfers whole business native");
            SaveGameManager.Current.Hour++;
            int requestedOffset=2;
            if(fault=="price")
            {
                f.Reg.retailPrices.Remove(price);f.Market.RefreshKnownOwnership();
                Check(!row.Desired && !row.Managed,"failed boundary revalidation keeps native authority");
                f.Reg.retailPrices.Add(price);f.Market.RefreshKnownOwnership();
                Check(!row.Desired,"same-hour repair does not repeat recovery work");
                SaveGameManager.Current.Hour++;requestedOffset++;
            }
            f.Market.RefreshKnownOwnership();
            Check(row.Desired && !row.Managed,"fresh complete contract requests future acquisition");
            Check(!row.Boundary(hour+requestedOffset,true,false,true),"revalidation never acquires its partly native hour");
            Check(!row.Boundary(hour+requestedOffset+1,true,true,true),"live native customers prevent reacquisition");
            SaveGameManager.Current.Hour++;
            Check(row.Boundary(hour+requestedOffset+1,true,false,true),"a later drained boundary reacquires without native customers");
            f.Market.RefreshKnownOwnership();
            Check(f.Market.CanServe(f.BusinessId),"repaired shop recovers within the same session");
            double now=f.Market.EarliestMinute;var meal=f.Meal("recovered-"+fault);
            Check(f.Market.Buy(meal,now).Paid,"repaired original-product shop serves after safe reacquisition");
            Check(f.Market.Buy(meal,now).Paid && f.Reg.unprocessedCompletedOrders.Count==1,"replayed recovered sale never duplicates its order");
        }
        finally {SaveGameManager.Current.Hour=originalHour;}
        }
    }
}
