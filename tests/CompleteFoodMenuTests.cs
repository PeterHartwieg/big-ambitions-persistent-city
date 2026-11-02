using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

public sealed class CompleteFoodMenuTests
{
    private const string Dumplings = "foodtrucks:itemname_dumplings", Cheese = "foodtrucks:itemname_choppedcheese";
    private static FoodEligibilityImage Image(string[]? ids = null, int stock = 2)
    {
        ids ??= new[] { Dumplings, Cheese };
        var catalogue = new LoadedStockCatalogue(1);
        LoadedStockDefinition Definition(string id, bool retail = false, bool shelf = false, bool bag = false, bool counter = false)
            => new LoadedStockDefinition(id, retail, false, shelf, false, true, true, bag, counter, true, ids, null);
        foreach (string id in ids) catalogue.Add(Definition(id, retail: true));
        catalogue.Add(Definition("fixture:shelf", shelf: true));
        catalogue.Add(Definition("fixture:bag", bag: true));
        catalogue.Add(Definition("fixture:counter", counter: true));
        catalogue.Seal();
        var builder = new LoadedStockMenuBuilder(catalogue, 1, true, ids,
            ids.Select(id => new StockPriceFields(id, 3)).ToArray());
        for (int i = 0; i < ids.Length; i++) builder.AddHolder("s"+i, "s"+i, "fixture:shelf", true,
            new[] { new StockCargoFields(ids[i], stock, 0) });
        builder.AddHolder("c", "c", "fixture:counter", true, new[] { new StockCargoFields("fixture:bag", 1, 0) });
        return builder.Finish();
    }
    private static CompleteFoodMenuRefusal Check(FoodEligibilityImage image, FoodProductMetadataResolver? resolver = null,
        bool current = true, bool native = true, string[]? history = null, string[]? names = null)
        => CompleteFoodMenu.Check(image, resolver ?? FoodProductMetadataResolver.Empty, current, native,
            history ?? Array.Empty<string>(), names ?? Array.Empty<string>());

    [Theory]
    [InlineData(MealClass.Unknown)] [InlineData(MealClass.Drink)]
    [InlineData(MealClass.Bundle)] [InlineData(MealClass.NonFood)]
    public void OneExcludedAdvertisedEntryRefusesWholeMenu(MealClass excluded)
    {
        var metadata = new FoodProductMetadataOverride(excluded, null, 900, 100, 450);
        var resolver = new FoodProductMetadataResolver(new[] { new KeyValuePair<string,FoodProductMetadataOverride>(Cheese, metadata) });
        Assert.Equal(CompleteFoodMenuRefusal.MealClass, Check(Image(), resolver));
        Assert.Equal(CompleteFoodMenuRefusal.None, Check(Image(new[] { Dumplings }), resolver));
    }
    [Fact]
    public void DefaultFoodNeedsStockImageAndIndependentNativeContract()
    {
        Assert.Equal(CompleteFoodMenuRefusal.None, Check(Image()));
        Assert.Equal(CompleteFoodMenuRefusal.None, Check(Image(stock: 0))); // known zero, never a sale promise
        Assert.Equal(CompleteFoodMenuRefusal.Dirty, Check(Image(), current: false));
        Assert.Equal(CompleteFoodMenuRefusal.NativeContract, Check(Image(), native: false));
        Assert.Equal(CompleteFoodMenuRefusal.StockContract, Check(Image(Enumerable.Range(0,9).Select(i=>"fixture:"+i).ToArray())));
    }
    [Fact]
    public void AdmissionPreflightsNinthHistoryCellAnd257thName()
    {
        Assert.Equal(CompleteFoodMenuRefusal.None, Check(Image(), history: new[] { Dumplings, Cheese }));
        Assert.Equal(CompleteFoodMenuRefusal.HistoryCapacity, Check(Image(), history: Enumerable.Range(0,7).Select(i=>"old:"+i).ToArray()));
        Assert.Equal(CompleteFoodMenuRefusal.NameCapacity, Check(Image(), names: Enumerable.Range(0,255).Select(i=>"old:"+i).ToArray()));
        Assert.Equal(CompleteFoodMenuRefusal.None, Check(Image(), names: Enumerable.Range(0,254).Select(i=>"old:"+i).ToArray()));
    }
    [Theory]
    [InlineData(MealClass.Unknown,FoodMealClass.Unknown)] [InlineData(MealClass.Food,FoodMealClass.Food)]
    [InlineData(MealClass.Drink,FoodMealClass.Drink)] [InlineData(MealClass.Bundle,FoodMealClass.Bundle)]
    [InlineData(MealClass.NonFood,FoodMealClass.NonFood)]
    public void ExplicitWireMappingPreservesSchema25(MealClass input, FoodMealClass expected)
    {
        var held = CompleteFoodMenu.Capture(new FoodProductMetadata(input,4,1,2,3));
        Assert.Equal(2, held.Revision); Assert.Equal(expected, held.MealClass);
        Assert.Equal(4, held.TasteBand); Assert.Equal(1, held.BreakfastFitCents);
        Assert.Equal(2, held.LunchFitCents); Assert.Equal(3, held.DinnerFitCents);
        Assert.Throws<ArgumentOutOfRangeException>(()=>CompleteFoodMenu.WireClass((MealClass)99));
    }
    [Fact]
    public void DirtyWithdrawalKeepsEndingHourUntilDrainedAndLaterBoundary()
    {
        var authority = new FoodAuthority(); authority.Observe("business", true, 12);
        Assert.False(authority.Boundary(12,true,false,true)); Assert.True(authority.Boundary(13,true,false,true));
        authority.Observe("business", false, 13);
        Assert.False(authority.CanServe); Assert.True(authority.OwnsHour(13));
        Assert.False(authority.Boundary(14,false,false,true)); Assert.True(authority.OwnsHour(14));
        Assert.True(authority.Boundary(14,true,false,true)); Assert.False(authority.OwnsHour(14));
        authority.Observe("business",true,14);
        Assert.False(authority.Boundary(15,true,true,true)); Assert.True(authority.Boundary(15,true,false,true));
    }
    [Fact]
    public void LoadedNamesDeliberatelyKeepTheirStricterBackslashRefusal()
    {
        const string historical = "mod:product\\variant";
        Assert.True(FoodNameIdentity.IsCanonical(historical));
        Assert.False(LoadedStockLimits.CanonicalName(historical));
        foreach (string value in new[] { Dumplings, Cheese, "mod:product😀", "mod:product ", "mod:Product" })
            Assert.Equal(FoodNameIdentity.IsCanonical(value), LoadedStockLimits.CanonicalName(value));
    }
}
