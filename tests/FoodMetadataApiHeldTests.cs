using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    [Theory]
    [InlineData(FoodMetadataApiTests.Dumplings)] [InlineData(FoodMetadataApiTests.ChoppedCheese)]
    public void E5e_HeldSchema25TupleAndWholeWireSurviveProviderChangesUnloadAndReload(string name)
    {
        var (city, _) = S17_Full();
        var decision = city.State.Food!.Residents.Single(m=>m.Decision!=null).Decision!;
        var offer = decision.Shops[0].Offers[0]; offer.ProductId = name; decision.ProductId = name;
        city.State.MenuPolicy = FoodNameIdentity.DynamicWire;
        var registry = FoodMetadataApiTests.Registry(); using var provider = FoodMetadataApiTests.Provider(registry);
        var input = FoodMetadataApiTests.Input(); input.MealClass = MealClass.Food;
        Assert.True(provider.TryRegisterProductMetadata(name,input,1,out _));
        var catalogue = FoodMetadataApiTests.Catalogue(name);
        var resolver = FoodProductMetadataResolver.Empty.WithApi(registry.Publish(catalogue));
        offer.Metadata = resolver.Capture(name,FoodMetadataApiTests.Traits,957,"pc:1");
        string held = DevelopmentChain.Write(city.State);
        Assert.Equal(CityState.CurrentSchema,city.State.Schema);
        input.MealClass = MealClass.Bundle; input.TasteBand = 0; input.BreakfastFitCents = 5000;
        Assert.True(provider.TryRegisterProductMetadata(name,input,1,out _));
        var next = FoodProductMetadataResolver.Empty.WithApi(registry.Publish(catalogue)).Capture(name,FoodMetadataApiTests.Traits,957,"pc:1");
        Assert.Equal(FoodMealClass.Bundle,next.MealClass); Assert.Equal(0,next.TasteBand); Assert.Equal(5000,next.BreakfastFitCents);
        Assert.Equal(held,DevelopmentChain.Write(city.State));
        provider.Dispose(); registry.ClearOnUnload();
        var read = DevelopmentChain.Read(held); Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);
        Assert.Equal(held,DevelopmentChain.Write(read.State!));
        var captured = read.State!.Food!.Residents.Single(m=>m.Decision!=null).Decision!.Shops[0].Offers[0].Metadata!;
        Assert.Equal(FoodMealClass.Food,captured.MealClass); Assert.Equal(4,captured.TasteBand); Assert.Equal(12,captured.BreakfastFitCents);
        registry.BindPublicationThread();
        var defaults=FoodProductMetadataResolver.Empty.WithApi(registry.Publish(catalogue)).Capture(name,FoodMetadataApiTests.Traits,957,"pc:1");
        Assert.Equal(900,defaults.BreakfastFitCents);
        // E5d's dynamic execution refusal remains intact; E5b owns executable admission/replay wiring.
        Assert.Throws<SaveValidationException>(()=>SaveCodec.RequireExecutableCapabilities(held));
    }
}
