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
    private static FoodProductMetadata Read(string fields) => Parse(Entry(fields)).FoodProductOverrides.Resolver.Resolve(Dumplings,Traits);
    private static EvaluatedFoodOffer Offer(string product, MealKind meal, double memory=0) => new("shop",product,Money.FromCents(100),2,1,10,90,null,null,meal,memory);

    [Theory]
    [InlineData("")] [InlineData("\"MealClass\":null")] [InlineData("\"MealClass\":12")]
    [InlineData("\"MealClass\":\"food\"")] [InlineData("\"MealClass\":\"Food \"")]
    [InlineData("\"MealClass\":{}")]
    public void AbsentAndInvalidClassesStayUnknown(string fields)
    {
        var m=Read(fields);
        Assert.Equal(MealClass.Unknown,m.MealClass);
        Assert.Equal((2,900,100,450),(m.TasteBand,m.BreakfastFitCents,m.LunchFitCents,m.DinnerFitCents));
    }
    [Theory]
    [InlineData("Unknown",MealClass.Unknown)] [InlineData("Food",MealClass.Food)] [InlineData("Drink",MealClass.Drink)]
    [InlineData("Bundle",MealClass.Bundle)] [InlineData("NonFood",MealClass.NonFood)]
    public void ClassIsExactAuthoredMetadataOnly(string text, MealClass expected)
        => Assert.Equal(expected,Read("\"MealClass\":"+JsonConvert.ToString(text)).MealClass);

    public static IEnumerable<object[]> MealsAndWeights()
    {
        foreach (var meal in new[]{MealKind.Breakfast,MealKind.Lunch,MealKind.Dinner})
            foreach (double weight in new[]{0,.25,.5,1,2,3}) yield return new object[]{meal,weight};
    }
    [Theory] [MemberData(nameof(MealsAndWeights))]
    public void NeutralFitIsMedianForEveryMealAtSupportedTasteWeights(MealKind meal,double weight)
    {
        var settings=new FoodChoiceSettings {FoodChoiceTasteWeight=weight,FoodChoiceWeightFloor=.001};
        var resolver=Parse(Entry("\"MealClass\":\"Food\"")).FoodProductOverrides.Resolver;
        var neutral=resolver.Resolve(Dumplings,Traits);
        int[] fits={900,100,450};
        Assert.Equal(fits[(int)meal-1],neutral.Penalty(meal));
        var originals=new[]{FoodProducts.Croissant,FoodProducts.Burger,FoodProducts.HotDog};
        var originalWeights=originals.Select(p=>FoodChoicePolicy.Weight(Offer(p,meal),Traits,Money.FromCents(1000),settings)).OrderBy(x=>x).ToArray();
        double actual=FoodChoicePolicy.Weight(Offer(Dumplings,meal),Traits,Money.FromCents(1000),settings,neutral);
        Assert.Equal(originalWeights[1],actual,12);
        // Compare each original, including full A3 floor/temperature/memory and rank ordering.
        foreach (string product in originals)
        {
            int fit=FoodProducts.Penalty(meal,product);
            double old=FoodChoicePolicy.Weight(Offer(product,meal),Traits,Money.FromCents(1000),settings);
            Assert.Equal(Math.Sign(weight*(fit-neutral.Penalty(meal))),Math.Sign(actual-old));
        }
        if (weight==1) Assert.Equal(new[]{5.5,1.5,3.25}[(int)meal-1],1+weight*neutral.Penalty(meal)/200.0);
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
                    var m=configured.Resolver.Resolve(offers[i].ProductId,traits);
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
    public void ArbitraryNamesUseNeutralDefaultsWithoutWireCodeHash(string name)
    {
        var m=FoodProductMetadataResolver.Empty.Resolve(name,Traits);
        Assert.Equal(FoodProductMetadataResolver.Neutral,m);
        Assert.Equal(0,m.Penalty(MealKind.Legacy));
        Assert.Equal(name,ResidentInsight.DefaultProductName(name));
        Assert.False(FoodProducts.Known(name));
    }
    [Fact]
    public void OrdinalKeysHaveNoTrimmingAliasesOrNamespaceFallback()
    {
        var table=Parse(Entry("\"MealClass\":\"Food\",\"TasteBand\":4")).FoodProductOverrides;
        Assert.Equal(4,table.Resolver.Resolve(Dumplings,Traits).TasteBand);
        foreach (string other in new[]{Dumplings.ToUpperInvariant()," "+Dumplings,Dumplings+" ","ba:itemname_dumplings","itemname_dumplings"})
            Assert.Equal(FoodProductMetadataResolver.Neutral,table.Resolver.Resolve(other,Traits));
        var logs=new List<string>();
        table.ReportUnloadedOnce(new HashSet<string>(StringComparer.Ordinal),logs.Add);
        table.ReportUnloadedOnce(new HashSet<string>(StringComparer.Ordinal),logs.Add);
        Assert.Single(logs); Assert.Contains("1 unloaded",logs[0]);
    }
    [Theory]
    [InlineData("\"TasteBand\":99,\"BreakfastFitCents\":-1,\"LunchFitCents\":\"Infinity\",\"DinnerFitCents\":\"NaN\"",4,0,5000,450)]
    [InlineData("\"TasteBand\":1.5,\"BreakfastFitCents\":{},\"LunchFitCents\":null,\"DinnerFitCents\":[]",2,900,100,450)]
    [InlineData("\"TasteBand\":\"-Infinity\",\"BreakfastFitCents\":\"5000.1\",\"LunchFitCents\":\"12\",\"DinnerFitCents\":0",0,5000,12,0)]
    public void NumbersClampOrDefaultAndLogOnce(string fields,int taste,int breakfast,int lunch,int dinner)
    {
        var table=Parse(Entry(fields)).FoodProductOverrides;
        var m=table.Resolver.Resolve(Dumplings,Traits);
        Assert.Equal((taste,breakfast,lunch,dinner),(m.TasteBand,m.BreakfastFitCents,m.LunchFitCents,m.DinnerFitCents));
        var logs=new List<string>(); table.ReportOnce(logs.Add); table.ReportOnce(logs.Add); Assert.Single(logs);
    }
    [Theory]
    [InlineData("\"MealClass\":\"Food\",\"TasteBand\":4,\"typo\":0")]
    [InlineData("\"MealClass\":\"Food\",\"MealClass\":\"Food\"")]
    [InlineData("\"MealClass\":\"Food\",\"TasteBand\":4,\"tasteband\":4")]
    public void UnknownOrDuplicateEntryFieldsDefaultWholeEntry(string fields)
        => Assert.Equal(FoodProductMetadataResolver.Neutral,Read(fields));
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
        Assert.Equal(1,Parse(Entry("",new string('x',256))).FoodProductOverrides.Resolver.Count);
        Assert.Equal(0,Parse(Entry("",new string('x',257))).FoodProductOverrides.Resolver.Count);
        Assert.Equal(0,Parse(Entry("\"unknown\":"+JsonConvert.ToString(new string('x',524288)))).FoodProductOverrides.Resolver.Count);
        Assert.True(FoodProductMetadataResolver.ValidName("mod:🍲"));
        Assert.False(FoodProductMetadataResolver.ValidName("mod:\ud800"));
        Assert.False(FoodProductMetadataResolver.ValidName("mod:\udc00"));
        Assert.False(FoodProductMetadataResolver.ValidName("   "));
        var duplicate=JsonConvert.DeserializeObject<PrototypeSettings>("{\"FoodProductOverrides\":"+Entry("")+",\"FoodProductOverrides\":{},\"MaximumPedestrians\":37}")!;
        Assert.Equal(0,duplicate.FoodProductOverrides.Resolver.Count); Assert.Equal(37,duplicate.MaximumPedestrians);
        Assert.ThrowsAny<JsonException>(()=>Parse("{"));
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
            _=resolver.Resolve(name,Traits);
            Assert.Equal(before,MemoryWire.Encode(memory));
            Assert.Equal(remembered,FoodMemoryPolicy.Contribution(memory,3,1,Traits,settings));
            Assert.Equal(unseen,FoodMemoryPolicy.Contribution(memory,4,1,Traits,settings));
            var offer=Offer(name,MealKind.Lunch,remembered);
            var s=new FoodChoiceSettings();
            double baseWeight=FoodChoicePolicy.Weight(Offer(name,MealKind.Lunch),Traits,Money.FromCents(1000),s,resolver.Resolve(name,Traits));
            Assert.Equal(baseWeight*Math.Exp(remembered),FoodChoicePolicy.Weight(offer,Traits,Money.FromCents(1000),s,resolver.Resolve(name,Traits)),12);
        }
    }
    [Fact]
    public void ResolverCopiesInputAndDoesNotAllocateOnWarmResolveOrWeights()
    {
        var entries=new Dictionary<string,FoodProductMetadata> {{Dumplings,new(MealClass.Food,2,900,100,450)}};
        var resolver=new FoodProductMetadataResolver(entries); entries.Clear();
        var offer=Offer(Dumplings,MealKind.Breakfast);var s=new FoodChoiceSettings();var allowance=Money.FromCents(1000);
        for(int i=0;i<20000;i++) _=FoodChoicePolicy.Weight(offer,Traits,allowance,s,resolver.Resolve(Dumplings,Traits));
        long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++) _=FoodChoicePolicy.Weight(offer,Traits,allowance,s,resolver.Resolve(Dumplings,Traits));
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-bytes);
        Assert.Equal(MealClass.Food,resolver.Resolve(Dumplings,Traits).MealClass);
    }
}
