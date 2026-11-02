using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodMetadataApiTests
{
    internal const string Dumplings = "foodtrucks:itemname_dumplings", ChoppedCheese = "foodtrucks:itemname_choppedcheese";
    internal static readonly ResidentTraits Traits = new() { Bands = "0123401234" };
    internal static LoadedStockCatalogue Catalogue(params string[] names)
    {
        var result = new LoadedStockCatalogue(1);
        foreach (string name in names) Assert.True(result.Add(new LoadedStockDefinition(name, true, false, false, false, true, true, false, false, true, null, null)));
        result.Seal(); return result;
    }
    internal static FoodProductMetadataRegistry Registry()
    { var registry = new FoodProductMetadataRegistry(); registry.BindPublicationThread(); return registry; }
    internal static FoodProductMetadataProvider Provider(FoodProductMetadataRegistry registry, string id = "test:provider")
    { Assert.True(registry.TryCreateProvider(id, out var provider, out var reason), reason); return provider; }
    internal static FoodProductMetadataInput Input() => new() { MealClass = MealClass.Drink, TasteBand = 4, BreakfastFitCents = 12, LunchFitCents = 34, DinnerFitCents = 56 };
    internal static FoodProductMetadata Resolve(FoodProductMetadataSnapshot image, string name = Dumplings)
        => FoodProductMetadataResolver.Empty.WithApi(image).Resolve(name, Traits, 957, "pc:1");
    private static void Register(FoodProductMetadataProvider provider, string name, FoodProductMetadataInput input)
        => Assert.True(provider.TryRegisterProductMetadata(name, input, 1, out var reason), reason);

    [Fact]
    public void TestDoubleProviderCopiesInputsAndReplacesAtomicallyWithoutChangingPublishedImages()
    {
        var registry = Registry(); using var provider = Provider(registry);
        var catalogue = Catalogue(Dumplings); var input = Input();
        var empty = registry.Publish(catalogue);
        Register(provider, Dumplings, input); input.TasteBand = 0; input.MealClass = MealClass.Bundle;
        Assert.Equal(MealClass.Food, Resolve(empty).MealClass);
        var first = registry.Publish(catalogue);
        Assert.Equal(new FoodProductMetadata(MealClass.Drink, 4, 12, 34, 56), Resolve(first));
        Register(provider, Dumplings, Input()); Assert.Same(first, registry.Publish(catalogue));
        Register(provider, Dumplings, input);
        Assert.Equal(MealClass.Drink, Resolve(first).MealClass);
        var second = registry.Publish(catalogue);
        Assert.Equal(MealClass.Bundle, Resolve(second).MealClass); Assert.Equal(0, Resolve(second).TasteBand);
        Assert.Equal(1, registry.ProductCount);
    }

    private static void OnWorker(Action action)
    {
        Exception? error = null;
        var thread = new Thread(()=> { try { action(); } catch(Exception caught) { error=caught; } });
        thread.Start(); thread.Join(); Assert.Null(error);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(2)] [InlineData(int.MaxValue)]
    public void UnsupportedVersionsNeverChangePreviousOrNextPublication(int version)
    {
        var registry = Registry(); using var provider = Provider(registry); var catalogue = Catalogue(Dumplings);
        Register(provider, Dumplings, Input()); var image = registry.Publish(catalogue);
        Assert.False(provider.TryRegisterProductMetadata(Dumplings, new(), version, out var reason));
        Assert.Equal("incompatible-api-version", reason); Assert.Same(image, registry.Publish(catalogue));
    }

    public static IEnumerable<object[]> InvalidInputs()
    {
        yield return new object[] { new FoodProductMetadataInput { MealClass = (MealClass)(-1) } };
        yield return new object[] { new FoodProductMetadataInput { MealClass = (MealClass)5 } };
        yield return new object[] { new FoodProductMetadataInput { TasteBand = -1 } };
        yield return new object[] { new FoodProductMetadataInput { TasteBand = 5 } };
        foreach (int n in new[] { -1, 5001 })
        {
            yield return new object[] { new FoodProductMetadataInput { BreakfastFitCents = n } };
            yield return new object[] { new FoodProductMetadataInput { LunchFitCents = n } };
            yield return new object[] { new FoodProductMetadataInput { DinnerFitCents = n } };
        }
    }
    [Theory] [MemberData(nameof(InvalidInputs))]
    public void InvalidApiFieldsRejectWholeClaimWithoutClampingOrLosingPriorData(FoodProductMetadataInput input)
    {
        var registry = Registry(); using var provider = Provider(registry); var catalogue = Catalogue(Dumplings);
        Register(provider, Dumplings, Input()); var image = registry.Publish(catalogue);
        Assert.False(provider.TryRegisterProductMetadata(Dumplings, input, 1, out var reason));
        Assert.Equal("invalid-metadata", reason); Assert.Same(image, registry.Publish(catalogue));
        Assert.False(provider.TryRegisterProductMetadata(ChoppedCheese, input, 1, out _));
        Assert.Equal(1, registry.ProductCount);
    }
    [Theory]
    [InlineData("")] [InlineData(" ")] [InlineData("bad|id")] [InlineData("bad\nid")]
    [InlineData("bad\\id")]
    public void InvalidNamesCannotConsumeProviderOrProductBudget(string name)
    {
        var registry = Registry(); Assert.False(registry.TryCreateProvider(name, out _, out _));
        using var provider = Provider(registry); int bytes = registry.BudgetUtf8Bytes;
        Assert.False(provider.TryRegisterProductMetadata(name, Input(), 1, out _));
        Assert.Equal(0, registry.ProductCount); Assert.Equal(bytes, registry.BudgetUtf8Bytes);
    }
    [Fact]
    public void ScalarEndpointsAndEmptyPartialMetadataAreAccepted()
    {
        var registry = Registry(); using var provider = Provider(registry);
        Register(provider, Dumplings, new() { MealClass = MealClass.Unknown, TasteBand = 0, BreakfastFitCents = 0, LunchFitCents = 5000 });
        var m = Resolve(registry.Publish(Catalogue(Dumplings)));
        Assert.Equal(new FoodProductMetadata(MealClass.Unknown, 0, 0, 5000, 450), m);
        Register(provider, Dumplings, new());
        Assert.Equal(FoodProductMetadataResolver.Empty.Resolve(Dumplings, Traits, 957, "pc:1"), Resolve(registry.Publish(Catalogue(Dumplings))));
    }
    [Fact]
    public void MetadataCannotRegisterDefinitionsOrRescueAnIneligibleLoadedProduct()
    {
        var registry=Registry(); using var p=Provider(registry);
        Register(p,Dumplings,new(){MealClass=MealClass.Food});
        var catalogue=new LoadedStockCatalogue(1);
        Assert.Equal(0,catalogue.Count); Assert.Equal(0,registry.Publish(catalogue).Count);
        catalogue.Add(new LoadedStockDefinition(Dumplings,false,true,false,false,true,true,false,false,false,null,null));
        catalogue.Seal();
        Assert.Equal(MealClass.Food,Resolve(registry.Publish(catalogue)).MealClass);
        var menu=new LoadedStockMenuBuilder(catalogue,1,true,new[]{Dumplings},new[]{new StockPriceFields(Dumplings,1)},furnitureCount:0);
        Assert.Equal(FoodEligibilityRefusal.RetailType,menu.Finish().Refusal);
        Assert.Equal(1,catalogue.Count);
        Assert.False(p.TryRegisterProductMetadata(Dumplings,null!,1,out var reason)); Assert.Equal("invalid-metadata",reason);
    }

    [Fact]
    public void Utf8BudgetAlsoRejectsAtomicallyAndReleasesAllChargesOnUnload()
    {
        var registry=Registry(); bool full=false; string reason="";
        for(int i=0;i<256 && !full;i++)
        {
            if(!registry.TryCreateProvider(i.ToString("D3")+new string('\u4e00',253),out var p,out reason)) { full=true;break; }
            for(int j=0;j<256;j++)
            {
                string name=j.ToString("D3")+new string('\u4e00',253);
                int before=registry.BudgetUtf8Bytes;
                if(!p.TryRegisterProductMetadata(name,new(),1,out reason) && reason=="registry-budget")
                { Assert.Equal(before,registry.BudgetUtf8Bytes);full=true;break; }
            }
        }
        Assert.True(full); Assert.Equal("registry-budget",reason);
        Assert.True(registry.BudgetUtf8Bytes>registry.BudgetCharacters);
        Assert.InRange(registry.BudgetUtf8Bytes,524000,524288);
        registry.ClearOnUnload(); Assert.Equal(2,registry.BudgetUtf8Bytes);
    }

    [Fact]
    public void ConflictInvalidatesPriorProviderUnderEveryRegistrationPermutationAndOnlyResolvesAtBoundary()
    {
        foreach (int[] order in new[] { new[] {0,1,2}, new[] {0,2,1}, new[] {1,0,2}, new[] {1,2,0}, new[] {2,0,1}, new[] {2,1,0} })
        {
            var registry = Registry(); var catalogue = Catalogue(Dumplings);
            var providers = Enumerable.Range(0,3).Select(i => Provider(registry, "provider:"+i)).ToArray();
            FoodProductMetadataSnapshot? first = null;
            foreach (int i in order)
            {
                bool accepted = providers[i].TryRegisterProductMetadata(Dumplings, new() { TasteBand = i }, 1, out var reason);
                Assert.Equal(first == null, accepted);
                Assert.Equal(first == null ? "accepted" : FoodProductMetadataRegistry.ConflictReason, reason);
                var image = registry.Publish(catalogue);
                if (first == null) first = image;
                else Assert.Equal(FoodProductMetadataRegistry.ConflictReason, image.Reason(Dumplings));
            }
            var conflict = registry.Publish(catalogue);
            Assert.Equal(FoodProductMetadataResolver.Empty.Resolve(Dumplings, Traits, 957, "pc:1"), Resolve(conflict));
            providers[0].Dispose(); providers[1].Dispose();
            Assert.Equal(FoodProductMetadataRegistry.ConflictReason, conflict.Reason(Dumplings));
            Assert.Equal(2, Resolve(registry.Publish(catalogue)).TasteBand);
            Assert.Equal(order[0], Resolve(first!).TasteBand);
            providers[2].Dispose(); Assert.Equal(0, registry.ProductCount);
            Assert.Equal(2, registry.BudgetCharacters); Assert.Equal(2, registry.BudgetUtf8Bytes);
        }
    }
    [Fact]
    public void IdenticalDifferentProvidersAlsoFailClosedAndDuplicateProviderIdsCannotReplaceOwner()
    {
        var registry = Registry(); using var a = Provider(registry, "a"); using var b = Provider(registry, "b");
        Assert.False(registry.TryCreateProvider("a", out _, out var reason)); Assert.Equal("provider-already-registered", reason);
        Register(a, Dumplings, Input());
        Assert.False(b.TryRegisterProductMetadata(Dumplings, Input(), 1, out reason));
        Assert.Equal(FoodProductMetadataRegistry.ConflictReason, registry.Publish(Catalogue(Dumplings)).Reason(Dumplings));
    }

    [Fact]
    public void BoundsAreAtomicAndBookkeepingIsChargedEvenForConflicts()
    {
        var registry = Registry(); using var p = Provider(registry, new string('p',256));
        Assert.False(registry.TryCreateProvider(new string('p',257), out _, out _));
        Assert.False(p.TryRegisterProductMetadata(new string('n',257), Input(), 1, out _));
        Assert.False(p.TryRegisterProductMetadata("bad\ud800", Input(), 1, out _));
        Assert.False(registry.TryCreateProvider("bad\udfff", out _, out _));
        for (int i=0;i<256;i++) Register(p, i.ToString("D3")+new string('x',253), Input());
        int budget=registry.BudgetCharacters;
        Assert.False(p.TryRegisterProductMetadata("extra:name", Input(), 1, out var reason)); Assert.Equal("product-bound", reason);
        Assert.Equal(budget,registry.BudgetCharacters);
        var others=Enumerable.Range(0,255).Select(i=>Provider(registry,"other:"+i)).ToArray();
        Assert.False(registry.TryCreateProvider("extra:provider",out _,out reason)); Assert.Equal("provider-bound",reason);
        foreach(var q in others) q.Dispose(); p.Dispose(); Assert.Equal(2,registry.BudgetCharacters);
        using var a=Provider(registry,"a");
        string name="x"+new string('\u2028',255); Register(a,name,Input());
        using var b=Provider(registry,"b");
        Assert.False(b.TryRegisterProductMetadata(name,Input(),1,out reason));
        Assert.Equal(FoodProductMetadataRegistry.ConflictReason,reason);
        Assert.Equal(128,registry.BudgetCharacters-(2+65+65+64+1531+128));
        bool full=false;
        for(int i=0;i<254;i++)
        {
            if (!registry.TryCreateProvider(i.ToString("D3")+new string('\u2028',253),out var q,out reason)) { full=true; break; }
            for(int j=0;j<256;j++)
            {
                string n=j==0 ? name : "name:"+j;
                int before=registry.BudgetCharacters;
                bool ok=q.TryRegisterProductMetadata(n,Input(),1,out reason);
                if(!ok && reason=="registry-budget") { Assert.Equal(before,registry.BudgetCharacters); full=true; break; }
            }
            if(full) break;
        }
        Assert.True(full); Assert.Equal("registry-budget",reason);
        Assert.InRange(registry.BudgetCharacters,1,524288); Assert.InRange(registry.BudgetUtf8Bytes,1,524288);
        registry.ClearOnUnload(); Assert.Equal(2,registry.BudgetCharacters); Assert.Equal(0,registry.ProviderCount);
    }

    [Fact]
    public void UnloadedLateRegisteredRemovedReturnedAndClearedProvidersUseFreshUncapturedImagesOnly()
    {
        var registry=Registry(); var p=Provider(registry); Register(p,Dumplings,Input()); Register(p,ChoppedCheese,Input());
        var absent=registry.Publish(Catalogue()); Assert.Equal("unloaded-product",absent.Reason(Dumplings)); Assert.Equal(0,absent.Count);
        var loaded=registry.Publish(Catalogue(Dumplings,ChoppedCheese)); Assert.Equal(2,loaded.Count);
        p.Dispose(); p.Dispose(); Assert.False(p.TryRegisterProductMetadata(Dumplings,Input(),1,out var reason));
        Assert.Equal("provider-unregistered",reason); Assert.Equal(2,loaded.Count);
        Assert.Equal(0,registry.Publish(Catalogue(Dumplings)).Count);
        var returned=Provider(registry); Register(returned,Dumplings,Input());
        Assert.Equal(MealClass.Drink,Resolve(registry.Publish(Catalogue(Dumplings))).MealClass);
        registry.ClearOnUnload(); Assert.False(returned.TryRegisterProductMetadata(Dumplings,Input(),1,out _));
        registry.BindPublicationThread(); Assert.Equal(0,registry.Publish(Catalogue(Dumplings)).Count);
    }
    [Fact]
    public void PublicationAndUnloadRequireHostThreadWhileProviderStagingCanUseWorkerThread()
    {
        var registry=Registry(); using var p=Provider(registry); var catalogue=Catalogue(Dumplings);
        OnWorker(()=>Register(p,Dumplings,Input()));
        OnWorker(()=> {
            Assert.Throws<InvalidOperationException>(()=>registry.Publish(catalogue));
            Assert.Throws<InvalidOperationException>(()=>registry.ClearOnUnload());
            Assert.Throws<InvalidOperationException>(()=>registry.BindPublicationThread());
        });
        Assert.Equal(MealClass.Drink,Resolve(registry.Publish(catalogue)).MealClass);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\"MealClass\":false,\"TasteBand\":1.5,\"BreakfastFitCents\":{},\"LunchFitCents\":null,\"DinnerFitCents\":\"NaN\"")]
    [InlineData("\"MealClass\":\"Food\",\"typo\":1")]
    [InlineData("\"MealClass\":\"Food\",\"TasteBand\":1,\"TasteBand\":2")]
    public void MissingRejectedFieldsAndRejectedEntriesFallThroughToApiBeforeDefaults(string fields)
    {
        var registry=Registry(); using var p=Provider(registry); Register(p,Dumplings,Input());
        string json="{\"FoodProductOverrides\":{\""+Dumplings+"\":{"+fields+"}}}";
        var config=JsonConvert.DeserializeObject<PrototypeSettings>(json)!;
        var image=registry.Publish(Catalogue(Dumplings)); var resolver=config.FoodProductOverrides.Resolver.WithApi(image);
        Assert.Equal(Resolve(image),resolver.Resolve(Dumplings,Traits,957,"pc:1"));
        Assert.Equal(FoodProductMetadataSource.Api,resolver.Sources(Dumplings).MealClass);
        var reloaded=JsonConvert.DeserializeObject<PrototypeSettings>(JsonConvert.SerializeObject(config))!;
        Assert.Equal(Resolve(image),reloaded.FoodProductOverrides.Resolver.WithApi(image).Resolve(Dumplings,Traits,957,"pc:1"));
    }
    [Fact]
    public void ConfigWinsPerFieldAndClampedNumbersRemainExplicitWhilePartialApiFallsThroughToDefaults()
    {
        var registry=Registry(); using var p=Provider(registry); Register(p,Dumplings,new(){MealClass=MealClass.Bundle,TasteBand=4,LunchFitCents=42});
        string json="{\"FoodProductOverrides\":{\""+Dumplings+"\":{\"MealClass\":\"Drink\",\"TasteBand\":99,\"BreakfastFitCents\":\"-1\"}}}";
        var resolver=JsonConvert.DeserializeObject<PrototypeSettings>(json)!.FoodProductOverrides.Resolver.WithApi(registry.Publish(Catalogue(Dumplings)));
        Assert.Equal(new FoodProductMetadata(MealClass.Drink,4,0,42,450),resolver.Resolve(Dumplings,Traits,957,"pc:1"));
        var sources=resolver.Sources(Dumplings);
        Assert.Equal(FoodProductMetadataSource.Config,sources.MealClass); Assert.Equal(FoodProductMetadataSource.Config,sources.TasteBand);
        Assert.Equal(FoodProductMetadataSource.Config,sources.BreakfastFit); Assert.Equal(FoodProductMetadataSource.Api,sources.LunchFit);
        Assert.Equal(FoodProductMetadataSource.Default,sources.DinnerFit);
    }
    [Fact]
    public void OriginalThreeSavedTraitsAndFitsAreAuthoritativeDespiteConfigAndApi()
    {
        var registry=Registry(); using var p=Provider(registry);
        string[] names={FoodProducts.Croissant,FoodProducts.Burger,FoodProducts.HotDog};
        foreach(string name in names) Register(p,name,Input());
        var resolver=new FoodProductMetadataResolver(names.Select(n=>new KeyValuePair<string,FoodProductMetadataOverride>(n,new(MealClass.Bundle,0,5000,5000,5000))))
            .WithApi(registry.Publish(Catalogue(names)));
        foreach(string name in names)
        {
            Assert.Equal(FoodProductMetadataResolver.Empty.Resolve(name,Traits,957,"pc:1"),resolver.Resolve(name,Traits,957,"pc:1"));
            Assert.Equal(FoodProductMetadataSource.Original,resolver.Sources(name).MealClass);
        }
    }
    [Fact]
    public void WarmCleanPublicationResolveAndSourcesAllocateNothingAndCannotCallProviderCode()
    {
        var registry=Registry(); using var p=Provider(registry); Register(p,Dumplings,Input()); var catalogue=Catalogue(Dumplings);
        var image=registry.Publish(catalogue); var resolver=FoodProductMetadataResolver.Empty.WithApi(image);
        for(int i=0;i<20000;i++) { _=registry.Publish(catalogue); _=resolver.Resolve(Dumplings,Traits,957,"pc:1"); _=resolver.Sources(Dumplings); }
        long bytes=GC.GetAllocatedBytesForCurrentThread(); long publications=registry.Publications;
        for(int i=0;i<10000;i++) { _=registry.Publish(catalogue); _=resolver.Resolve(Dumplings,Traits,957,"pc:1"); _=resolver.Sources(Dumplings); }
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-bytes); Assert.Equal(publications,registry.Publications);
        // Public registration surface accepts only strings, nullable scalar DTO, version and out reason.
        Assert.DoesNotContain(typeof(FoodProductMetadataInput).GetProperties(), prop=>typeof(Delegate).IsAssignableFrom(prop.PropertyType));
        Assert.All(typeof(FoodProductMetadataProvider).GetMethods().Where(m=>m.DeclaringType==typeof(FoodProductMetadataProvider)),
            m=>Assert.DoesNotContain(m.GetParameters(),arg=>typeof(Delegate).IsAssignableFrom(arg.ParameterType)));
    }
    [Fact]
    public void EmptyProviderLifecycleDoesNotInvalidateAnUnchangedImage()
    {
        var registry=Registry();var catalogue=Catalogue(Dumplings);var image=registry.Publish(catalogue);
        using var empty=Provider(registry,"empty"); Assert.Same(image,registry.Publish(catalogue));
        empty.Dispose(); Assert.Same(image,registry.Publish(catalogue));
        using var real=Provider(registry,"real");Register(real,Dumplings,Input());
        var active=registry.Publish(catalogue);Assert.NotSame(image,active);
        using var other=Provider(registry,"other");Assert.Same(active,registry.Publish(catalogue));
        other.Dispose();Assert.Same(active,registry.Publish(catalogue));
    }
}
