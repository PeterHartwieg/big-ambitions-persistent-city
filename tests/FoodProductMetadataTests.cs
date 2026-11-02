using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodProductMetadataTests
{
    private const string Dumplings="foodtrucks:itemname_dumplings", ChoppedCheese="foodtrucks:itemname_choppedcheese";
    private static readonly ResidentTraits Traits=new() {Bands="2222222222"};
    private static PrototypeSettings Parse(string table) => JsonConvert.DeserializeObject<PrototypeSettings>("{\"FoodProductOverrides\":"+table+",\"MaximumPedestrians\":37}")!;
    private static string Entry(string fields, string name=Dumplings) => "{"+JsonConvert.ToString(name)+":{"+fields+"}}";
    private static FoodProductMetadata Read(string fields) => Parse(Entry(fields)).FoodProductOverrides.Resolver.Resolve(Dumplings,Traits,957,"consumer");
    private static EvaluatedFoodOffer Offer(string product, MealKind meal, double memory=0) => new("shop",product,Money.FromCents(100),2,1,10,90,null,null,meal,memory);

    [Fact]
    public void BackslashCanonicalNamesKeepConfigSeededTasteAndCaptureCompatibility()
    {
        const string name=@"mod:item\special";
        Assert.True(FoodNameIdentity.IsCanonical(name));
        Assert.True(FoodProductMetadataResolver.ValidName(name));
        string json="{"+JsonConvert.ToString(name)+":{\"MealClass\":\"Drink\",\"BreakfastFitCents\":37},"
            +JsonConvert.ToString(Dumplings)+":{\"TasteBand\":3}}";
        var table=Parse(json).FoodProductOverrides;
        Assert.False(table.InvalidTable); Assert.Equal(2,table.Resolver.Count);
        Assert.Equal(3,table.Resolver.Resolve(Dumplings,Traits,957,"consumer").TasteBand);
        foreach(var resolver in new[]{FoodProductMetadataResolver.Empty,table.Resolver})
        {
            var resolved=resolver.Resolve(name,Traits,957,"consumer");
            var captured=resolver.Capture(name,Traits,957,"consumer");
            Assert.Equal(FoodProductMetadataResolver.DefaultTaste(957,"consumer",name),resolved.TasteBand);
            Assert.Equal(resolved.TasteBand,captured.TasteBand);
            Assert.Equal((FoodMealClass)resolved.MealClass,captured.MealClass);
            Assert.Equal(resolved.BreakfastFitCents,captured.BreakfastFitCents);
            Assert.Equal(resolved.LunchFitCents,captured.LunchFitCents);
            Assert.Equal(resolved.DinnerFitCents,captured.DinnerFitCents);
        }
        var configured=table.Resolver.Resolve(name,Traits,957,"consumer");
        Assert.Equal(MealClass.Drink,configured.MealClass); Assert.Equal(37,configured.BreakfastFitCents);
        var roundtrip=JsonConvert.DeserializeObject<PrototypeSettings>(JsonConvert.SerializeObject(Parse(json)))!;
        Assert.Equal(configured,roundtrip.FoodProductOverrides.Resolver.Resolve(name,Traits,957,"consumer"));
    }

    [Theory]
    [InlineData("")] [InlineData("\"MealClass\":null")] [InlineData("\"MealClass\":12")]
    [InlineData("\"MealClass\":\"food\"")] [InlineData("\"MealClass\":\"Food \"")]
    [InlineData("\"MealClass\":{}")]
    public void AbsentAndInvalidClassesDefaultToFood(string fields)
    {
        var m=Read(fields);
        Assert.Equal(MealClass.Food,m.MealClass);
        Assert.Equal((FoodProductMetadataResolver.DefaultTaste(957,"consumer",Dumplings),900,100,450),(m.TasteBand,m.BreakfastFitCents,m.LunchFitCents,m.DinnerFitCents));
    }
    [Theory]
    [InlineData("Unknown",MealClass.Unknown)] [InlineData("Food",MealClass.Food)] [InlineData("Drink",MealClass.Drink)]
    [InlineData("Bundle",MealClass.Bundle)] [InlineData("NonFood",MealClass.NonFood)]
    public void ClassIsExactAuthoredMetadataOnly(string text, MealClass expected)
        => Assert.Equal(expected,Read("\"MealClass\":"+JsonConvert.ToString(text)).MealClass);

    public static IEnumerable<object[]> MealsAndWeights()
    {
        foreach (var meal in new[]{MealKind.Breakfast,MealKind.Lunch,MealKind.Dinner})
            foreach (double weight in new[]{0,.25,.5,1,2,3})
                for(int band=0;band<5;band++) yield return new object[]{meal,weight,band};
    }
    [Theory] [MemberData(nameof(MealsAndWeights))]
    public void DefaultFitIsMedianForEveryMealAtSupportedTasteWeights(MealKind meal,double weight,int band)
    {
        var settings=new FoodChoiceSettings {FoodChoiceTasteWeight=weight,FoodMenuNewTasteWeight=weight,FoodChoiceWeightFloor=.001};
        var traits=new ResidentTraits {Bands=new string((char)('0'+band),10)};
        var resolver=Parse(Entry("\"MealClass\":\"Food\",\"TasteBand\":"+band)).FoodProductOverrides.Resolver;
        var metadata=resolver.Resolve(Dumplings,traits,957,"consumer");
        int[] fits={900,100,450};
        Assert.Equal(fits[(int)meal-1],metadata.Penalty(meal));
        var originals=new[]{FoodProducts.Croissant,FoodProducts.Burger,FoodProducts.HotDog};
        var originalWeights=originals.Select(p=>FoodChoicePolicy.Weight(Offer(p,meal),traits,Money.FromCents(1000),settings)).OrderBy(x=>x).ToArray();
        double actual=FoodChoicePolicy.Weight(Offer(Dumplings,meal),traits,Money.FromCents(1000),settings,metadata);
        Assert.Equal(originalWeights[1],actual,12);
        // Compare each original, including full A3 floor/temperature/memory and rank ordering.
        foreach (string product in originals)
        {
            int fit=FoodProducts.Penalty(meal,product);
            double old=FoodChoicePolicy.Weight(Offer(product,meal),traits,Money.FromCents(1000),settings);
            Assert.Equal(Math.Sign(weight*(fit-metadata.Penalty(meal))),Math.Sign(actual-old));
        }
        if (weight==1) Assert.Equal(new[]{5.5,1.5,3.25}[(int)meal-1],1+weight*metadata.Penalty(meal)/200.0);
    }
    [Fact]
    public void OriginalThreeWeightsAndDrawsAreUnchangedAndOverridesCannotReplaceSavedTaste()
    {
        var configured=Parse(Entry("\"MealClass\":\"Drink\",\"TasteBand\":0,\"BreakfastFitCents\":5000",FoodProducts.Croissant)).FoodProductOverrides;
        var settings=new FoodChoiceSettings();
        for (int seed=0;seed<50;seed++)
        {
            var traits=ResidentTraits.Generate(seed,"consumer");
            foreach (var meal in new[]{MealKind.Legacy,MealKind.Breakfast,MealKind.Lunch,MealKind.Dinner})
            {
                var offers=new[]{FoodProducts.Croissant,FoodProducts.Burger,FoodProducts.HotDog}.Select(p=>Offer(p,meal)).ToArray();
                var draw=new FoodDrawContext(seed,"consumer",1,meal,"home",1);
                double best=double.PositiveInfinity; int selected=-1;
                for (int i=0;i<offers.Length;i++)
                {
                    var m=configured.Resolver.Resolve(offers[i].ProductId,traits,seed,"consumer");
                    Assert.Equal(MealClass.Food,m.MealClass);
                    Assert.Equal(FoodChoicePolicy.Taste(traits,offers[i].ProductId),m.TasteBand);
                    double historical=FoodChoicePolicy.Weight(offers[i],traits,Money.FromCents(1000),settings);
                    double resolved=FoodChoicePolicy.Weight(offers[i],traits,Money.FromCents(1000),settings,m);
                    Assert.Equal(historical,resolved);
                    double rank=FoodDrawContext.Rank(draw.OfferHash("shop",offers[i].ProductId),resolved);
                    if(rank<best) {best=rank;selected=i;}
                }
                Assert.Equal(selected,FoodChoicePolicy.Select(offers,traits,Money.FromCents(1000),draw,settings,false).Index);
            }
        }
        var logs=new List<string>(); configured.ReportOnce(logs.Add); configured.ReportOnce(logs.Add);
        Assert.Single(logs);
    }
    [Theory]
    [InlineData(Dumplings)] [InlineData(ChoppedCheese)] [InlineData("ba:itemname_donut")]
    [InlineData("anothermod:unknown")] [InlineData("BA:itemname_burger")]
    public void ArbitraryNamesUseFoodAndSeededTasteWithoutWireCodeHash(string name)
    {
        var m=FoodProductMetadataResolver.Empty.Resolve(name,Traits,957,"consumer");
        Assert.Equal(MealClass.Food,m.MealClass);
        Assert.Equal(FoodProductMetadataResolver.DefaultTaste(957,"consumer",name),m.TasteBand);
        Assert.Equal((900,100,450),(m.BreakfastFitCents,m.LunchFitCents,m.DinnerFitCents));
        Assert.Equal(0,m.Penalty(MealKind.Legacy));
        Assert.Equal(name,ResidentInsight.DefaultProductName(name));
        Assert.False(FoodProducts.Known(name));
    }
    [Fact]
    public void OrdinalKeysHaveNoTrimmingAliasesOrNamespaceFallback()
    {
        var table=Parse(Entry("\"MealClass\":\"Food\",\"TasteBand\":4")).FoodProductOverrides;
        Assert.Equal(4,table.Resolver.Resolve(Dumplings,Traits,957,"consumer").TasteBand);
        foreach (string other in new[]{Dumplings.ToUpperInvariant()," "+Dumplings,Dumplings+" ","ba:itemname_dumplings","itemname_dumplings"})
            Assert.Equal(FoodProductMetadataResolver.Empty.Resolve(other,Traits,957,"consumer"),table.Resolver.Resolve(other,Traits,957,"consumer"));
        var logs=new List<string>();
        table.ReportUnloadedOnce(new HashSet<string>(StringComparer.Ordinal),logs.Add);
        table.ReportUnloadedOnce(new HashSet<string>(StringComparer.Ordinal),logs.Add);
        Assert.Single(logs); Assert.Contains("1 unloaded",logs[0]);
    }
    [Theory]
    [InlineData("\"TasteBand\":99,\"BreakfastFitCents\":-1,\"LunchFitCents\":\"Infinity\",\"DinnerFitCents\":\"NaN\"",4,0,5000,450)]
    [InlineData("\"TasteBand\":1.5,\"BreakfastFitCents\":{},\"LunchFitCents\":null,\"DinnerFitCents\":[]",-1,900,100,450)]
    [InlineData("\"TasteBand\":\"-Infinity\",\"BreakfastFitCents\":\"5000.1\",\"LunchFitCents\":\"12\",\"DinnerFitCents\":0",0,5000,12,0)]
    public void NumbersClampOrDefaultAndLogOnce(string fields,int taste,int breakfast,int lunch,int dinner)
    {
        var table=Parse(Entry(fields)).FoodProductOverrides;
        var m=table.Resolver.Resolve(Dumplings,Traits,957,"consumer");
        Assert.Equal((taste==-1 ? FoodProductMetadataResolver.DefaultTaste(957,"consumer",Dumplings) : taste,breakfast,lunch,dinner),(m.TasteBand,m.BreakfastFitCents,m.LunchFitCents,m.DinnerFitCents));
        var logs=new List<string>(); table.ReportOnce(logs.Add); table.ReportOnce(logs.Add); Assert.Single(logs);
    }
    [Theory]
    [InlineData("\"MealClass\":\"Food\",\"TasteBand\":4,\"typo\":0")]
    [InlineData("\"MealClass\":\"Food\",\"MealClass\":\"Food\"")]
    [InlineData("\"MealClass\":\"Food\",\"TasteBand\":4,\"tasteband\":4")]
    public void UnknownOrDuplicateEntryFieldsDefaultWholeEntry(string fields)
        => Assert.Equal(FoodProductMetadataResolver.Empty.Resolve(Dumplings,Traits,957,"consumer"),Read(fields));
    [Theory]
    [InlineData("null")] [InlineData("[]")] [InlineData("12")]
    [InlineData("{\"a\":{},\"a\":{}}")]
    [InlineData("{\"\":{}}")]
    [InlineData("{\"bad|name\":{}}")]
    [InlineData("{\"bad\\nname\":{}}")]
    public void InvalidTableIsRejectedWithoutDiscardingOtherConfig(string json)
    {
        var settings=Parse(json); var logs=new List<string>();
        Assert.Equal(37,settings.MaximumPedestrians); Assert.Equal(0,settings.FoodProductOverrides.Resolver.Count);
        settings.FoodProductOverrides.ReportOnce(logs.Add);settings.FoodProductOverrides.ReportOnce(logs.Add);
        Assert.Single(logs);
    }
    [Fact]
    public void AllTableAndNameBoundsAreAtomic()
    {
        string Table(int count,int length=1) => "{"+string.Join(",",Enumerable.Range(0,count).Select(i=>JsonConvert.ToString(i.ToString(CultureInfo.InvariantCulture)+new string('x',length))+":{}"))+"}";
        Assert.Equal(256,Parse(Table(256)).FoodProductOverrides.Resolver.Count);
        Assert.Equal(0,Parse(Table(257)).FoodProductOverrides.Resolver.Count);
        string escapedMaximum="{"+string.Join(",",Enumerable.Range(0,256).Select(i=>JsonConvert.ToString(i.ToString(CultureInfo.InvariantCulture).PadLeft(3,'0')+new string('\u2028',253))
            +":{\"MealClass\":\"Food\",\"TasteBand\":2,\"BreakfastFitCents\":900,\"LunchFitCents\":100,\"DinnerFitCents\":450}"))+"}";
        Assert.Equal(256,Parse(escapedMaximum).FoodProductOverrides.Resolver.Count);
        Assert.Equal(1,Parse(Entry("",new string('x',256))).FoodProductOverrides.Resolver.Count);
        Assert.Equal(0,Parse(Entry("",new string('x',257))).FoodProductOverrides.Resolver.Count);
        Assert.Equal(0,Parse(Entry("\"unknown\":"+JsonConvert.ToString(new string('x',524288)))).FoodProductOverrides.Resolver.Count);
        Assert.Equal(0,Parse(Entry("\"unknown\":"+JsonConvert.ToString(new string('\u4e00',180000)))).FoodProductOverrides.Resolver.Count);
        Assert.Equal(0,Parse(Entry("\"unknown\":"+JsonConvert.ToString(new string('\u2028',90000)))).FoodProductOverrides.Resolver.Count);
        Assert.True(FoodProductMetadataResolver.ValidName("mod:🍲"));
        Assert.False(FoodProductMetadataResolver.ValidName("mod:\ud800"));
        Assert.False(FoodProductMetadataResolver.ValidName("mod:\udc00"));
        Assert.False(FoodProductMetadataResolver.ValidName("   "));
        var duplicate=JsonConvert.DeserializeObject<PrototypeSettings>("{\"FoodProductOverrides\":"+Entry("")+",\"FoodProductOverrides\":{},\"MaximumPedestrians\":37}")!;
        Assert.Equal(0,duplicate.FoodProductOverrides.Resolver.Count); Assert.Equal(37,duplicate.MaximumPedestrians);
        Assert.ThrowsAny<JsonException>(()=>Parse("{"));
    }
    [Fact]
    public void EmptyAndAbsentTablesHaveNoAdjustmentDiagnostics()
    {
        foreach(var settings in new[]{Parse("{}"),JsonConvert.DeserializeObject<PrototypeSettings>("{}")!})
        {
            var logs=new List<string>();settings.FoodProductOverrides.ReportOnce(logs.Add);
            settings.FoodProductOverrides.ReportOnce(logs.Add);Assert.Empty(logs);
            Assert.Equal(FoodProductMetadataResolver.Empty.Resolve(Dumplings,Traits,957,"consumer"),settings.FoodProductOverrides.Resolver.Resolve(Dumplings,Traits,957,"consumer"));
        }
    }
    [Fact]
    public void EnumerationOrderAndUnusedMetadataGrowthDoNotChangeWeightsOrWinner()
    {
        string small="{"+JsonConvert.ToString(Dumplings)+":{\"MealClass\":\"Food\"},"+JsonConvert.ToString(ChoppedCheese)+":{\"MealClass\":\"Food\",\"TasteBand\":3}}";
        string large="{"+string.Join(",",Enumerable.Range(0,254).Select(i=>JsonConvert.ToString("unused:"+i)+":{}"))+","+small.Substring(1);
        var a=Parse(small).FoodProductOverrides.Resolver;
        var b=Parse(large).FoodProductOverrides.Resolver;
        var names=new[]{FoodProducts.Croissant,FoodProducts.Burger,FoodProducts.HotDog,Dumplings,ChoppedCheese};
        for(int seed=0;seed<50;seed++)
        {
            var traits=ResidentTraits.Generate(seed,"resident");var draw=new FoodDrawContext(seed,"resident",1,MealKind.Lunch,"home",1);
            string Winner(IEnumerable<string> products,FoodProductMetadataResolver resolver) => products.Select(p=>new {Product=p,
                Rank=FoodDrawContext.Rank(draw.OfferHash("shop",p),FoodChoicePolicy.Weight(Offer(p,MealKind.Lunch),traits,Money.FromCents(1000),new FoodChoiceSettings(),resolver.Resolve(p,traits,seed,"resident")))})
                .OrderBy(x=>x.Rank).ThenBy(x=>x.Product,StringComparer.Ordinal).First().Product;
            Assert.Equal(Winner(names,a),Winner(names.Reverse(),b));
            foreach(string name in names) Assert.Equal(a.Resolve(name,traits,seed,"resident"),b.Resolve(name,traits,seed,"resident"));
        }
    }
    [Fact]
    public void ConfigAndCatalogueMetadataChangesCannotCreateMemoryOrNoveltyEvents()
    {
        var memory=new ConsumerMemory {Entries=new(){new ShopMemory {BusinessRef=3,Day=1,Served=3,Affinity=4,Outcome=ShopExperience.Served}}};
        string before=MemoryWire.Encode(memory);
        var settings=new FoodMemorySettings();
        double remembered=FoodMemoryPolicy.Contribution(memory,3,1,Traits,settings), unseen=FoodMemoryPolicy.Contribution(memory,4,1,Traits,settings);
        foreach(string name in new[]{Dumplings,ChoppedCheese,"mod:removed","mod:returned"})
        {
            var resolver=Parse(Entry("\"MealClass\":\"Food\"",name)).FoodProductOverrides.Resolver;
            _=resolver.Resolve(name,Traits,957,"consumer");
            Assert.Equal(before,MemoryWire.Encode(memory));
            Assert.Equal(remembered,FoodMemoryPolicy.Contribution(memory,3,1,Traits,settings));
            Assert.Equal(unseen,FoodMemoryPolicy.Contribution(memory,4,1,Traits,settings));
            var offer=Offer(name,MealKind.Lunch,remembered);
            var s=new FoodChoiceSettings();
            double baseWeight=FoodChoicePolicy.Weight(Offer(name,MealKind.Lunch),Traits,Money.FromCents(1000),s,resolver.Resolve(name,Traits,957,"consumer"));
            Assert.Equal(baseWeight*Math.Exp(remembered),FoodChoicePolicy.Weight(offer,Traits,Money.FromCents(1000),s,resolver.Resolve(name,Traits,957,"consumer")),12);
        }
    }
    [Fact]
    public void ResolverCopiesInputAndDoesNotAllocateOnWarmResolveOrWeights()
    {
        var entries=new Dictionary<string,FoodProductMetadataOverride> {{Dumplings,new(MealClass.Food,2,900,100,450)}};
        var resolver=new FoodProductMetadataResolver(entries); entries.Clear();
        var offer=Offer(Dumplings,MealKind.Breakfast);var s=new FoodChoiceSettings();var allowance=Money.FromCents(1000);
        for(int i=0;i<20000;i++)
        {
            _=FoodChoicePolicy.Weight(offer,Traits,allowance,s,resolver.Resolve(Dumplings,Traits,957,"consumer"));
            _=FoodChoicePolicy.Weight(offer,Traits,allowance,s,FoodProductMetadataResolver.Empty.Resolve(Dumplings,Traits,957,"consumer"));
        }
        long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++)
        {
            _=FoodChoicePolicy.Weight(offer,Traits,allowance,s,resolver.Resolve(Dumplings,Traits,957,"consumer"));
            _=FoodChoicePolicy.Weight(offer,Traits,allowance,s,FoodProductMetadataResolver.Empty.Resolve(Dumplings,Traits,957,"consumer"));
        }
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-bytes);
        Assert.Equal(MealClass.Food,resolver.Resolve(Dumplings,Traits,957,"consumer").MealClass);
    }
    [Theory]
    [InlineData(Dumplings)] [InlineData(ChoppedCheese)]
    public void SeededTasteVariesByResidentAndProductWithA2Distribution(string product)
    {
        var counts=new int[5]; var products=new HashSet<int>(); int differences=0;
        for(int i=0;i<10000;i++)
        {
            string id="pc:"+i.ToString(CultureInfo.InvariantCulture);
            int taste=FoodProductMetadataResolver.DefaultTaste(957,id,product);
            counts[taste]++;
            if(taste!=FoodProductMetadataResolver.DefaultTaste(957,id,"another:product")) differences++;
            products.Add(taste);
            // Reconstructed resolver/settings/traits represent fresh loads; no persisted taste array.
            Assert.Equal(taste,Parse("{}").FoodProductOverrides.Resolver.Resolve(product,new ResidentTraits {Bands=Traits.Bands},957,id).TasteBand);
        }
        Assert.Equal(5,products.Count); Assert.InRange(differences,5000,8500);
        int[] expected={1000,2000,4000,2000,1000};
        for(int i=0;i<5;i++) Assert.InRange(counts[i],expected[i]-200,expected[i]+200);
    }
    [Fact]
    public void TasteOverrideReplacesSeededDrawForAllResidentsAndRoundtripKeepsAbsence()
    {
        var table=Parse(Entry("\"TasteBand\":4")).FoodProductOverrides;
        for(int i=0;i<100;i++) Assert.Equal(4,table.Resolver.Resolve(Dumplings,Traits,i,"pc:"+i).TasteBand);
        var settings=Parse(Entry("\"MealClass\":\"Drink\""));
        string json=JsonConvert.SerializeObject(settings);
        var reloaded=JsonConvert.DeserializeObject<PrototypeSettings>(json)!;
        for(int i=0;i<100;i++)
            Assert.Equal(FoodProductMetadataResolver.DefaultTaste(957,"pc:"+i,Dumplings),reloaded.FoodProductOverrides.Resolver.Resolve(Dumplings,Traits,957,"pc:"+i).TasteBand);
    }
    [Fact]
    public void NameHashMatchesIndependentFixedAlgorithmAndLengthDelimitedVectors()
    {
        // Reference strings intentionally allocate here; production streams the same UTF-16 units.
        foreach(int seed in new[]{0,957,-1,int.MaxValue})
        foreach(string id in new[]{"pc:1","pc:20","a|b:","resident:\uD83C\uDF72"})
        foreach(string name in new[]{Dumplings,ChoppedCheese,"mod:\uD83C\uDF72","x"})
        {
            string key="pc:product-name-taste:v1|"+id.Length.ToString(CultureInfo.InvariantCulture)+":"+id+"|"+name.Length.ToString(CultureInfo.InvariantCulture)+":"+name;
            uint hash=unchecked(2166136261u^(uint)seed);
            unchecked {foreach(char c in key) hash=(hash^c)*16777619u; hash^=hash>>16; hash*=0x7feb352du; hash^=hash>>15;}
            uint percentile=hash%100;
            int expected=percentile<10 ? 0 : percentile<30 ? 1 : percentile<70 ? 2 : percentile<90 ? 3 : 4;
            Assert.Equal(expected,FoodProductMetadataResolver.DefaultTaste(seed,id,name));
        }
    }

    [Fact]
    public void LargeIntegerTokensCannotBypassEscapedTableBudgetBeforeClamping()
    {
        Assert.Equal(4,Read("\"TasteBand\":184467440737095516160").TasteBand);
        string numericFields=string.Join(",",new[]{"TasteBand","BreakfastFitCents","LunchFitCents","DinnerFitCents"}
            .Select(field=>JsonConvert.ToString(field)+":"+new string('9',300)));
        string table="{"+string.Join(",",Enumerable.Range(0,256).Select(i=>JsonConvert.ToString(i.ToString("D3",CultureInfo.InvariantCulture)
            +new string('\u2028',253))+":{"+numericFields+"}"))+"}";
        Assert.True(table.Length>FoodProductMetadataResolver.MaximumTableCharactersAndBytes);
        var settings=Parse(table); var logs=new List<string>();
        Assert.Equal(37,settings.MaximumPedestrians);
        Assert.Equal(0,settings.FoodProductOverrides.Resolver.Count);
        settings.FoodProductOverrides.ReportOnce(logs.Add); settings.FoodProductOverrides.ReportOnce(logs.Add);
        Assert.Single(logs);
    }

}
