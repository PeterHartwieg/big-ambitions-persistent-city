using BigAmbitions.Items;
using PersistentCity.Runtime;

static partial class Program
{
    static void IcecreamChecks(IcecreamFeasibilitySnapshot cold)
    {
        Check(Product == "ba:itemname_icecream" && cold.ProductId == Product, "pinned icecream identity");
        Refusal(x => x.retailPrices.Clear(), "advertised product/price");
        // These are independently verified identifiers, never an asserted recipe.
        foreach (string input in new[] { "ba:itemname_icecreammixture", "ba:itemname_cream",
            "ba:itemname_milk", "ba:itemname_sugar", "ba:itemname_flavorsyrup" })
        {
            Refusal(x => x.cachedAvailableProducts[0] = input, "advertised product/price");
            Refusal(x => { ItemsGetter.Items["fixture-shelf"].producerSettings.itemsToProduce = new[] { Product };
                x.itemInstances["s0"].cargoInstances[0].itemName = input; }, "Converted input");
        }
        Refusal(x => x.itemInstances["counter"].cargoInstances.Clear(), "Shared, nested");
        var r = Fixture();
        const string holder = "ba:itemname_icecreamcounter";
        var definition = ItemsGetter.Items["fixture-shelf"];
        ItemsGetter.Items.Remove("fixture-shelf"); ItemsGetter.Items[holder] = definition;
        r.itemInstances["s0"].itemName = holder;
        var direct = IcecreamFeasibilityReader.Read(r);
        Check(direct.DirectStockFieldsVerified && direct.Equipment.Single() == holder,
            "conditional showcase fields authorize stock, never the equipment name");
        definition.type = ItemType.EmployeeWorkstation;
        definition.producerSettings.itemsToProduce = new[] { Product };
        string before = Fingerprint(r);
        Check(IcecreamFeasibilityReader.Read(r).Refusal == "Workstation/shared or converted input: unsupported",
            "named workstation with finished cargo refused");
        r.itemInstances["s0"].cargoInstances[0].itemName = "ba:itemname_icecreammixture";
        Check(IcecreamFeasibilityReader.Read(r).Refusal == "Workstation/shared or converted input: unsupported",
            "named workstation with mixture and output list refused");
        r.itemInstances["s0"].cargoInstances[0].itemName = Product;
        Check(before == Fingerprint(r), "refused named holder unmodified");
        definition.type = ItemType.ShowcaseShelf;
        definition.itemsThatCanShowcase = new[] { "ba:itemname_icecreammixture" };
        Check(IcecreamFeasibilityReader.Read(r).Refusal == "Equipment whitelist mismatch", "named holder whitelist mismatch");
        foreach (string other in new[] { DonutFeasibilityReader.Product, CupcakeFeasibilityReader.Product, SaladFeasibilityReader.Product })
        {
            Refusal(x => ItemsGetter.Items["fixture-shelf"].itemsThatCanShowcase = new[] { other }, "whitelist mismatch");
            r = Fixture(); ItemsGetter.Items[other] = new() { type = ItemType.RetailProduct };
            r.cachedAvailableProducts.Add(other); r.retailPrices.Add(new() { itemName = other, price = 12 });
            r.itemInstances["s0"].cargoInstances[0].itemName = other;
            ItemsGetter.Items["fixture-shelf"].itemsThatCanShowcase = new[] { other };
            Check(IcecreamFeasibilityReader.Read(r).Refusal == "No direct stocked candidate showcase shelf observed",
                "other candidate never substitutes for icecream");
            var independent = other == DonutFeasibilityReader.Product ? (DirectStockFeasibilitySnapshot)DonutFeasibilityReader.Read(r)
                : other == CupcakeFeasibilityReader.Product ? CupcakeFeasibilityReader.Read(r) : SaladFeasibilityReader.Read(r);
            Check(independent.DirectStockFieldsVerified && independent.RawProductUnits == 7, "other descriptor reads independently");
            before = Fingerprint(r);
            for (int i = 0; i < 100; i++)
            { IcecreamFeasibilityReader.Read(r); DonutFeasibilityReader.Read(r); CupcakeFeasibilityReader.Read(r); SaladFeasibilityReader.Read(r); }
            Check(before == Fingerprint(r), "all four share metadata only, never stock or results");
        }
    }
}
