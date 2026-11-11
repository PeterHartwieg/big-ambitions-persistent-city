using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    [Fact]
    public void E4_PolicyHeaderAndCompactHistoryRejectAtomicallyWithoutLosingLegacyIds()
    {
        var (city,_)=S17_Full(); string source=DevelopmentChain.Write(city.State);
        Assert.Contains("\"MenuPolicy\":",source);
        SaveCodec.RequireExecutableCapabilities(source);
        var loaded=DevelopmentChain.Read(source); Assert.True(loaded.Status==SaveReadStatus.Loaded,loaded.Error);
        Assert.Equal(source,DevelopmentChain.Write(loaded.State!));
        foreach(var value in new[]{JValue.CreateNull(),new JValue("2|unknown"),new JValue(1)})
        {
            var root=SaveWire.Parse(source);root["MenuPolicy"]=value;
            B0_Preserve(root.ToString(Formatting.None),city.State);
            Assert.Throws<SaveValidationException>(()=>SaveCodec.RequireExecutableCapabilities(root.ToString(Formatting.None)));
        }
        var history=new FoodShopDay { Day=0, PlaceId="shop", BusinessId="historical", Purchases=1, Revenue=Money.FromCents(1),
            PurchaseHours=new long[24], PurchaseMeals=new long[]{1,0,0,0},
            ProductSales={ new FoodProductSales{ProductId=FoodProducts.Burger,Purchases=1,Revenue=Money.FromCents(1),PurchaseMeals=new long[]{1,0,0,0}} },
            BuyerDistricts={ new FoodBuyerDistrict{DistrictId="a",Purchases=1} } };
        history.PurchaseHours[0]=1;city.State.Food!.History.Add(history);
        history.ProductSales[0].ProductId="historical-foreign-product|"+new string('x',230);
        string legacy=DevelopmentChain.Write(city.State);
        Assert.Equal(legacy,DevelopmentChain.Write(DevelopmentChain.Read(legacy).State!));
        history.ProductSales.Add(new FoodProductSales{ProductId="bad",Purchases=1});
        var data=new Dictionary<string,string>{{SaveStore.StateKey,legacy}};
        Assert.False(SaveStore.WriteTo(data,city.State).Success);Assert.Equal(legacy,data[SaveStore.StateKey]);
    }

    [Fact]
    public void E4_TastesArePureVersionedAndExistingBandsAndTuningStayExact()
    {
        var traits=new ResidentTraits{Bands="0412222222"};
        Assert.Equal(0,FoodMenuPolicy.Taste(traits,12,"consumer",FoodProducts.Croissant));
        Assert.Equal(4,FoodMenuPolicy.Taste(traits,12,"consumer",FoodProducts.Burger));
        Assert.Equal(1,FoodMenuPolicy.Taste(traits,12,"consumer",FoodProducts.HotDog));
        Assert.Equal(2,FoodMenuPolicy.Taste(traits,12,"consumer","unknown"));
        foreach(int code in Enumerable.Range(4,13))
        {
            int expected=FoodMenuPolicy.Taste(957,"consumer:2",code);
            uint percentile=StableRandom.Hash("pc:product-taste:v1|10:consumer:2|"+code,957)%100;
            int golden=percentile<10 ? 0 : percentile<30 ? 1 : percentile<70 ? 2 : percentile<90 ? 3 : 4;
            Assert.Equal(golden,expected);
            for(int i=0;i<20;i++) Assert.Equal(expected,FoodMenuPolicy.Taste(957,"consumer:2",code));
            Assert.InRange(expected,0,4);Assert.Null(FoodMenuPolicy.HealthTag(code));Assert.Null(FoodMenuPolicy.QualityTag(code));
        }
        Assert.Equal(10,ResidentTraits.Count);Assert.Equal(1,ResidentTraits.CurrentVersion);
        Assert.Equal(new[]{FoodProducts.Croissant,FoodProducts.Burger,FoodProducts.HotDog},FoodProducts.Ids);
        foreach(int code in new[]{4,5,6}) Assert.False(FoodProducts.Known(FoodMenuPolicy.Product(code)!));
        Assert.Equal(193,FoodDecisionTuning.Capture(LegacyFoodChoice.Settings()).ToWire().Length);
        Assert.Equal(241,FoodDecisionTuning.Capture(new FoodChoiceSettings()).ToWire().Length); // E5b5: eight is explicit
        var settings=new FoodChoiceSettings{FoodMenuMaximumOffersPerShop=8,FoodMenuNewTasteWeight=.5,FoodMenuHealthWeight=2};
        string held=FoodDecisionTuning.Capture(settings).ToWire();settings.FoodMenuMaximumOffersPerShop=1;
        Assert.True(FoodDecisionTuning.TryParse(held,out var replay));Assert.Equal(held,replay!.ToWire());
        Assert.Equal(241,held.Length);
    }

    [Theory]
    // E5b5 (Q1): the default and the fallback for an invalid value are the live limit of eight.
    [InlineData("{}",8,1,0)]
    [InlineData("{\"FoodMenuMaximumOffersPerShop\":99,\"FoodMenuNewTasteWeight\":\"Infinity\",\"FoodMenuHealthWeight\":-1}",8,2,0)]
    [InlineData("{\"FoodMenuMaximumOffersPerShop\":1.5,\"FoodMenuNewTasteWeight\":{},\"FoodMenuHealthWeight\":\"NaN\"}",8,1,0)]
    [InlineData("{\"FoodMenuMaximumOffersPerShop\":\"1\",\"FoodMenuNewTasteWeight\":\"0.5\",\"FoodMenuHealthWeight\":\"1.5\"}",1,.5,1.5)]
    public void E4_FlatSettingsLoadClampAndReload(string json,int limit,double taste,double health)
    {
        var settings=JsonConvert.DeserializeObject<PrototypeSettings>(json)!;var logs=new List<string>();settings.FoodChoice.Clamp(logs.Add);
        Assert.Equal(limit,settings.FoodMenuMaximumOffersPerShop);Assert.Equal(taste,settings.FoodMenuNewTasteWeight);Assert.Equal(health,settings.FoodMenuHealthWeight);
        Assert.InRange(Math.Min(FoodMenuPolicy.LiveMaximumOffers,settings.FoodMenuMaximumOffersPerShop),1,8);
        int count=logs.Count;settings.FoodChoice.Clamp(logs.Add);Assert.Equal(count,logs.Count);
    }
}
