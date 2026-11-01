using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;

static partial class Program
{
    static void E4MenuChecks()
    {
        using(var lower=new Fixture(maximumOffers:1))
        {
            Check(lower.State.Shops.Count==0 && lower.Market.Read(Minute).Count==0,"E4 configured limit one refuses whole two-product business");
        }
        using(var upper=new Fixture(maximumOffers:8))
        {
            Check(upper.State.Shops.Count==1 && upper.Market.Read(Minute).Single().Offers.Count==2,"E4 configured eight admits unchanged verified menu");
        }
        foreach(string product in new[]{"ba:itemname_donut","ba:itemname_cupcake","ba:itemname_salad","test:e4:7"})
        {
            using var f=new Fixture();
            f.Reg.cachedAvailableProducts.Add(product);
            int stock=f.Stock(Croissant),bags=f.Bags;
            Check(!NativeFoodMarket.Supported(f.Reg),"E4 reserved/test product never native supported: "+product);
            Check(!f.Market.CanServe(f.BusinessId),"E4 whole business cannot serve unsupported edit");
            Check(!f.Market.Buy(f.Meal("unsupported"),Minute).Paid,"E4 unsupported menu never books checkout");
            Check(f.Stock(Croissant)==stock && f.Bags==bags && f.Reg.unprocessedCompletedOrders.Count==0,"E4 refusal changes no native sale data");
        }
        using(var f=new Fixture(bags:1))
        {
            var menu=f.Market.Read(Minute).Single();
            Check(menu.Offers.All(o=>o.Units==1),"E4 offers expose shared last bag, never a multiplied bag balance");
            Check(f.Market.Buy(f.Meal("one"),Minute).Paid,"E4 first product uses last bag");
            Check(!f.Market.Buy(f.Meal("two",Burger,Money.FromCents(600)),Minute).Paid,"E4 second product revalidates same depleted bag pool");
            Check(f.Bags==0 && f.Reg.unprocessedCompletedOrders.Count==1,"E4 exactly one order from one shared bag");
        }
    }
}
