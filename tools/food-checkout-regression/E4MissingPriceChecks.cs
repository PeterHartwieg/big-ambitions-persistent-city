using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

static partial class Program
{
    // E5b supersedes E4's priced-subset behavior. Diagnostic filters retain their old semantics.
    static void E4MissingPriceChecks()
    {
        foreach (string unpriced in new[] { Burger, Croissant })
        {
            using var f = new Fixture();
            var meal = f.Meal("missing-price");
            f.Reg.retailPrices.RemoveAll(price => price.itemName == unpriced);
            Check(f.Market.CaptureBusiness(f.BusinessId, Minute) == null, "one missing price refuses the whole capture");
            Check(f.State.Shops.Single().ContainsProduct(unpriced), "withdrawal preserves saved scope identities");
            Check(!f.Market.CanServe(f.BusinessId), "missing price withdraws whole-business desired ownership");
            Check(!f.Market.Buy(meal, Minute).Paid, "priced remainder cannot create a native sale");
            Check(f.Market.NativePurchases == 0 && f.Reg.unprocessedCompletedOrders.Count == 0 && f.Bags == 3,
                "refused subset changes no stock, bag or order");
            Check(f.Receipt(meal) == FoodReceiptStatus.Missing, "refusal starts no receipt");
            f.Reg.retailPrices.Add(new() { itemName = unpriced, price = unpriced == Burger ? 6 : 3 });
            f.Market.RefreshKnownOwnership();
            Check(!f.Market.CanServe(f.BusinessId), "same-hour repair cannot bypass drained handoff and later reacquisition");
        }
        foreach (string fault in new[] { "other-price", "other-price-removed", "advertised", "duplicate", "selected-duplicate", "selected-removed", "missing-prices", "ninth" })
        {
            using var f = new Fixture();
            var meal = f.Meal("setup-menu-" + fault);
            Hooks.OpenCheck = () =>
            {
                if (fault == "other-price") f.Reg.retailPrices.Single(p => p.itemName == Burger).price = 9;
                if (fault == "other-price-removed") f.Reg.retailPrices.RemoveAll(p => p.itemName == Burger);
                if (fault == "advertised") f.Reg.cachedAvailableProducts.Remove(Burger);
                if (fault == "duplicate") f.Reg.cachedAvailableProducts[1] = Croissant;
                if (fault == "selected-duplicate") f.Reg.retailPrices.Add(new() {itemName=Croissant,price=3});
                if (fault == "selected-removed") f.Reg.retailPrices.RemoveAll(p=>p.itemName==Croissant);
                if (fault == "missing-prices") f.Reg.retailPrices=null!;
                if (fault == "ninth") f.Reg.cachedAvailableProducts.AddRange(Enumerable.Range(0,7).Select(i=>"other:"+i));
            };
            Check(!f.Market.Buy(meal, Minute).Paid, "entire menu is checked after native callbacks: " + fault);
            Check(f.Receipt(meal) == FoodReceiptStatus.Missing && f.Stock(Croissant) == 5 && f.Bags == 3
                && f.Reg.unprocessedCompletedOrders.Count == 0, "post-setup change precedes all effects: " + fault);
        }
        foreach (string fault in new[] {"hour", "day", "save"})
        {
            using var f=new Fixture(); var meal=f.Meal("late-clock-"+fault);
            var original=SaveGameManager.Current; int day=original.Day,hour=original.Hour;
            try
            {
                Hooks.OpenCheck=()=>
                {
                    if(fault=="hour") original.Hour++;
                    if(fault=="day") original.Day++;
                    if(fault=="save") SaveGameManager.Current=new Save();
                };
                Check(!f.Market.Buy(meal,Minute).Paid,"late accounting/save change refuses before receipt: "+fault);
                Check(FoodSaleReceipts.ReadStatus(original.modData,meal)==FoodReceiptStatus.Missing
                    && f.Bags==3 && f.Reg.unprocessedCompletedOrders.Count==0,"changed clock/city creates no native effect: "+fault);
            }
            finally {SaveGameManager.Current=original;original.Day=day;original.Hour=hour;}
        }
    }
}
