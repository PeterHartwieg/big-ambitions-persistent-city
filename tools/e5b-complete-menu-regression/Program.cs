using System.Diagnostics;
using System.Text.Json;
using BigAmbitions.Items;
using PersistentCity.Core;
using PersistentCity.Runtime;

// Conditional native field doubles only. This deliberately invokes dirty notifications
// explicitly: no claim that native mutation coverage or dynamic checkout is implemented.
static class Program
{
    const string Dumplings = "foodtrucks:itemname_dumplings", Cheese = "foodtrucks:itemname_choppedcheese";
    static readonly string[] Products = { FoodProducts.Croissant, FoodProducts.Burger, FoodProducts.HotDog,
        Dumplings, Cheese, "other:one", "other:two", "other:three" };
    static int checks;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    static BuildingRegistration Fixture()
    {
        ItemsGetter.Items = new(StringComparer.Ordinal); ItemsGetter.View = null; ItemsGetter.Unready = false;
        Helpers.BusinessTypeHelper.Data = new();
        foreach (string name in Products) ItemsGetter.Items[name] = new() { itemName = name, type = ItemType.RetailProduct };
        ItemsGetter.Items["holder"] = new() { itemName = "holder", type = ItemType.ShowcaseShelf, itemsThatCanShowcase = Products };
        ItemsGetter.Items["counter"] = new() { itemName = "counter", AuthoredTags = { "ba:itemtag_isfullservicecashregister" } };
        ItemsGetter.Items["bag"] = new() { itemName = "bag", AuthoredTags = { "ba:itemtag_isbag" } };
        var r = new BuildingRegistration { cachedAvailableProducts = Products.Reverse().ToList(),
            retailPrices = Products.Select(p => new RetailPrice { itemName = p, price = 3 }).ToList() };
        for (int i = 0; i < Products.Length; i++) r.itemInstances["s"+i] = new() { id = "s"+i, itemName = "holder",
            cargoInstances = new() { new() { itemName = Products[i], amount = 4 } } };
        r.itemInstances["c"] = new() { id = "c", itemName = "counter", cargoInstances = new() { new() { itemName = "bag", amount = 1 } } };
        return r;
    }
    static void Drain(LoadedCatalogueEligibilityReader reader) { int units=0; while(reader.Continue()) Check(++units<100000,"bounded work"); }
    static CompleteFoodMenuRefusal Admit(FoodEligibilityImage image, bool current=true, bool native=true, FoodProductMetadataResolver? resolver=null)
        => CompleteFoodMenu.Check(image,resolver ?? FoodProductMetadataResolver.Empty,current,native,Array.Empty<string>(),Array.Empty<string>());
    static FoodEligibilityImage Read(LoadedCatalogueEligibilityReader reader)
    { Check(reader.TryGet("shop",out var image),"complete publication"); return image; }
    static void Main()
    {
        var shop=Fixture(); using var reader=new LoadedCatalogueEligibilityReader(); reader.MarkShopDirty("shop",shop); Drain(reader);
        var first=Read(reader);
        Check(Admit(first)==CompleteFoodMenuRefusal.None,"eight actual-stock Food-by-default entries pass conditional admission");
        Check(Admit(first,native:false)==CompleteFoodMenuRefusal.NativeContract,"raw stock cannot establish native contract");
        Check(first.RawBagUnits==1 && first.Offers.Count==8,"one shared pool across eight offers");
        foreach(var excluded in new[]{MealClass.Drink,MealClass.Bundle,MealClass.NonFood,MealClass.Unknown})
        {
            var resolver=new FoodProductMetadataResolver(new[]{new KeyValuePair<string,FoodProductMetadataOverride>(Cheese,new(excluded,null,900,100,450))});
            Check(Admit(first,resolver:resolver)==CompleteFoodMenuRefusal.MealClass,"one excluded modded entry withdraws whole menu");
        }
        foreach(string product in new[]{Dumplings,Cheese})
        {
            var before=Enumerable.Range(0,100).Select(i=>FoodProductMetadataResolver.DefaultTaste(957,"pc:"+i,product)).ToArray();
            var definition=ItemsGetter.Items[product]; ItemsGetter.Items.Remove(product); reader.MarkCatalogueDirty();
            Check(!reader.TryGet("shop",out _),"removal withdraws cache immediately"); Drain(reader);
            Check(Admit(Read(reader))==CompleteFoodMenuRefusal.StockContract,"missing mod is whole-menu refusal");
            ItemsGetter.Items[product]=definition; reader.MarkCatalogueDirty(); Drain(reader);
            Check(Admit(Read(reader))==CompleteFoodMenuRefusal.None,"return requires fresh full image");
            Check(before.SequenceEqual(Enumerable.Range(0,100).Select(i=>FoodProductMetadataResolver.DefaultTaste(957,"pc:"+i,product))),"removal never remaps or rerolls names");
        }
        shop.retailPrices.RemoveAt(0); reader.MarkShopDirty("shop",shop);
        Check(!reader.TryGet("shop",out _),"dirty before capture and checkout");
        Check(Admit(first,current:false)==CompleteFoodMenuRefusal.Dirty,"held old image cannot authorize new effects"); Drain(reader);
        Check(Read(reader).Refusal==FoodEligibilityRefusal.Price,"missing-price subset refuses whole menu");
        shop=Fixture(); reader.MarkCatalogueDirty(); reader.MarkShopDirty("shop",shop); Drain(reader);
        // Zero finished stock is known, not a request to manufacture stock or change authority.
        shop.itemInstances["s3"].cargoInstances[0].amount=0; reader.MarkShopDirty("shop",shop); Drain(reader);
        Check(Admit(Read(reader))==CompleteFoodMenuRefusal.None,"zero stock preserves conditional shape");
        Check(Read(reader).Offers.Single(o=>o.ProductId==Dumplings).RawUnits==0,"zero remains zero");
        var image=Read(reader);
        long definitions=reader.DefinitionsRead,holders=reader.HoldersRead;
        var clean=Measure(()=> { if(!reader.TryGet("shop",out _)) throw new Exception("clean miss"); },10000);
        var admission=Measure(()=> { if(Admit(image)!=CompleteFoodMenuRefusal.None) throw new Exception("admission"); },10000);
        Check(reader.DefinitionsRead==definitions && reader.HoldersRead==holders,"clean admission has no catalogue or holder reads");
        var storm=Measure(()=>reader.MarkShopDirty("shop",shop),10000);
        Check(reader.PendingShops==1,"notification storms coalesce"); Drain(reader);
        using var backlog=new LoadedCatalogueEligibilityReader();
        for(int i=0;i<1024;i++) Check(backlog.MarkShopDirty("shop:"+i,shop),"bounded shop registration");
        var units=new List<double>(); var total=Stopwatch.StartNew();
        while(true) { var start=Stopwatch.GetTimestamp(); if(!backlog.Continue()) break; units.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
        total.Stop(); Check(backlog.PendingShops==0,"1024-shop backlog drains");
        Console.WriteLine(JsonSerializer.Serialize(new { checks, clean, admission, storm,
            backlog=new { units=units.Count,totalMilliseconds=total.Elapsed.TotalMilliseconds,timing=Summary(units),backlog.CataloguePasses,backlog.ShopPasses },
            nativeDynamicCheckout="INCOMPLETE: notification coverage and schema-25 execution remain refused; no game evidence",
            sharedNativeSetupCount="not measured here; actual historical checkout harness measures setup reuse separately" }));
    }
    static object Measure(Action action,int count)
    {
        for(int i=0;i<100;i++) action();
        var times=new List<double>(count); long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<count;i++) { long start=Stopwatch.GetTimestamp(); action(); times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
        bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
        return new {bytesPerCall=bytes/(double)count,timing=Summary(times)};
    }
    static object Summary(List<double> values)
    { values.Sort(); return new {p50=values[values.Count/2],p95=values[(int)(values.Count*.95)],max=values[^1],spikes30=values.Count(v=>v>=30)}; }
}
