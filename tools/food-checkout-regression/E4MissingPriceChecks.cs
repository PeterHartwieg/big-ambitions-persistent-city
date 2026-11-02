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
            Check(!f.Market.CanServe(f.BusinessId), "unverified mutation coverage cannot auto-reacquire a latched refusal");
        }
        foreach (string fault in new[] { "other-price", "other-price-removed", "advertised", "duplicate" })
        {
            using var f = new Fixture();
            var meal = f.Meal("setup-menu-" + fault);
            Hooks.OpenCheck = () =>
            {
                if (fault == "other-price") f.Reg.retailPrices.Single(p => p.itemName == Burger).price = 9;
                if (fault == "other-price-removed") f.Reg.retailPrices.RemoveAll(p => p.itemName == Burger);
                if (fault == "advertised") f.Reg.cachedAvailableProducts.Remove(Burger);
                if (fault == "duplicate") f.Reg.cachedAvailableProducts[1] = Croissant;
            };
            Check(!f.Market.Buy(meal, Minute).Paid, "entire menu is checked after native callbacks: " + fault);
            Check(f.Receipt(meal) == FoodReceiptStatus.Missing && f.Stock(Croissant) == 5 && f.Bags == 3
                && f.Reg.unprocessedCompletedOrders.Count == 0, "post-setup change precedes all effects: " + fault);
        }
    }
}
