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
    private static string E4_Old(CityState city, int schema)
    {
        var contextType=typeof(SaveCodec).GetNestedType("WireContext",BindingFlags.NonPublic)!;
        var context=contextType.GetMethod("FromState")!.Invoke(null,new object[]{city});
        var settings=(JsonSerializerSettings)typeof(SaveCodec).GetMethod("SettingsForSchema",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new[]{context,(object)schema})!;
        var root=JObject.Parse(JsonConvert.SerializeObject(city,settings)); root["Schema"]=schema;
        return root.ToString(Formatting.None);
    }

    [Theory] [InlineData(18)] [InlineData(19)] [InlineData(20)] [InlineData(22)]
    public void E4_ExactOldReaderMigrationAndForeignOrNewerShapesPreserveSource(int schema)
    {
        var (city,_)=S17_Full();
        string source=E4_Old(city.State,schema);
        var read=SaveCodec.Read(source); Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);
        Assert.Equal(schema,read.SourceSchema); Assert.Equal(23,read.State!.Schema);
        Assert.Equal(FoodMenuPolicy.Wire,read.State.MenuPolicy);
        Assert.Equal(source,E4_Old(read.State,schema));
        Assert.Equal(SaveCodec.Write(read.State),SaveCodec.Write(SaveCodec.Read(SaveCodec.Write(read.State)).State!));
        foreach(int unknown in new[]{21,24})
        {
            var root=JObject.Parse(source);root["Schema"]=unknown;
            string bad=root.ToString(Formatting.None);
            var data=new Dictionary<string,string>{{SaveStore.StateKey,bad}};
            Assert.NotEqual(SaveReadStatus.Loaded,SaveCodec.Read(bad).Status);
            Assert.Throws<SaveValidationException>(()=>SaveCodec.RequireExecutableCapabilities(bad));
            Assert.False(SaveStore.WriteTo(data,city.State).Success);Assert.Same(bad,data[SaveStore.StateKey]);
        }
        var foreign=JObject.Parse(source); foreign["MenuPolicy"]=FoodMenuPolicy.Wire;
        B0_Preserve(foreign.ToString(Formatting.None),city.State);
    }

    [Theory] [InlineData(18)] [InlineData(19)] [InlineData(20)] [InlineData(22)]
    public void E4_OldContractsRejectFifthScopeHistoryAndCapturedOffers(int schema)
    {
        var (city,_)=S17_Full();
        var history=new FoodShopDay {Day=0,PlaceId="shop",BusinessId="past",Purchases=1,PurchaseMeals=new long[]{1,0,0,0},
            ProductSales={new FoodProductSales{ProductId="past-product",Purchases=1,PurchaseMeals=new long[]{1,0,0,0}}},
            BuyerDistricts={new FoodBuyerDistrict{DistrictId="a",Purchases=1}}};
        history.PurchaseHours[0]=1;city.State.Food!.History.Add(history);
        string oldSource=E4_Old(city.State,schema);Assert.Equal(SaveReadStatus.Loaded,SaveCodec.Read(oldSource).Status);
        var original=JObject.Parse(oldSource);
        foreach(string damage in new[]{"scope","history","decision"})
        {
            var root=(JObject)original.DeepClone();
            if(damage=="scope") root["Food"]!["Shops"]![0]!["AdditionalProductIds"]=new JArray("p1","p2","p3","p4");
            else if(damage=="history")
            {
                var row=root["Food"]!["History"]![0]!;
                row["ProductSales"]=new JArray(Enumerable.Range(0,5).Select(_=>row["ProductSales"]![0]!.DeepClone()));
            }
            else
            {
                // ResidentMeal is the published fixed tuple; Decision is slot 9 (including revision).
                var decision=root["Food"]!["Residents"]!.Select(m=>m[9]).First(d=>d?.Type==JTokenType.Object)!;
                decision["Shops"]![0]!["Offers"]=new JArray(Enumerable.Range(0,5).Select(_=>decision["Shops"]![0]!["Offers"]![0]!.DeepClone()));
            }
            if(damage!="scope") Assert.Contains("Oversized collection",SaveCodec.Read(root.ToString(Formatting.None)).Error);
            B0_Preserve(root.ToString(Formatting.None),city.State);
        }
    }

    [Fact]
    public void E4_Schema20WeatherCapturesSurviveMigrationAndChangedLiveInputsExactly()
    {
        var (city, _, planner) = WeightedFoodChoiceTests.Ready(79, 12);
        city.AttachWeather(new FrameWeatherSource(() => 1, () => true));
        city.AdvanceFrameSlice(city.State.Food!.Residents.Single().DesiredAt, 1);
        var person = city.State.Citizens[0];
        person.W = new WeatherCommitment(city.State.Minute, WeatherSample.Rain, true);
        city.State.WeatherStartsDay = 1;
        string source = E4_Old(city.State, 20);
        var read = SaveCodec.Read(source);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(20, read.SourceSchema); Assert.Equal(23, read.State!.Schema);
        Assert.Equal(source, E4_Old(read.State, 20)); // every predecessor field, including held weather, is exact
        Assert.Equal(1, read.State.WeatherStartsDay);
        Assert.Equal(person.W.ToWire(), read.State.Citizens[0].W!.ToWire());
        var meal = Assert.Single(read.State.Food!.Residents);
        Assert.Equal(WeatherSample.Rain, meal.Memory.Weather);
        Assert.Equal(WeatherSample.Rain, meal.Decision!.Weather);
        Assert.Equal(1.3, meal.Decision.WeatherTravelMultiplier);
        string migrated = SaveCodec.Write(read.State);
        int reads = 0;
        var resumed = new CitySimulator(read.State, planner);
        resumed.AttachWeather(new FrameWeatherSource(() => 2, () => { reads++; return false; }),
            new WeatherSettings { Enabled = false, RainTravelWeightMultiplier = 2 });
        Assert.Equal(migrated, SaveCodec.Write(resumed.State)); Assert.Equal(0, reads);
        Assert.Equal(migrated, SaveCodec.Write(SaveCodec.Read(migrated).State!));
    }

    [Fact]
    public void E4_PolicyHeaderAndCompactHistoryRejectAtomicallyWithoutLosingLegacyIds()
    {
        var (city,_)=S17_Full(); string source=SaveCodec.Write(city.State);
        Assert.Contains("\"MenuPolicy\":",source);
        SaveCodec.RequireExecutableCapabilities(source);
        var loaded=SaveCodec.Read(source); Assert.True(loaded.Status==SaveReadStatus.Loaded,loaded.Error);
        Assert.Equal(source,SaveCodec.Write(loaded.State!));
        foreach(var value in new[]{JValue.CreateNull(),new JValue("2|unknown"),new JValue(1)})
        {
            var root=JObject.Parse(source);root["MenuPolicy"]=value;
            B0_Preserve(root.ToString(Formatting.None),city.State);
            Assert.Throws<SaveValidationException>(()=>SaveCodec.RequireExecutableCapabilities(root.ToString(Formatting.None)));
        }
        var history=new FoodShopDay { Day=0, PlaceId="shop", BusinessId="historical", Purchases=1, Revenue=Money.FromCents(1),
            PurchaseHours=new long[24], PurchaseMeals=new long[]{1,0,0,0},
            ProductSales={ new FoodProductSales{ProductId=FoodProducts.Burger,Purchases=1,Revenue=Money.FromCents(1),PurchaseMeals=new long[]{1,0,0,0}} },
            BuyerDistricts={ new FoodBuyerDistrict{DistrictId="a",Purchases=1} } };
        history.PurchaseHours[0]=1;city.State.Food!.History.Add(history);
        history.ProductSales[0].ProductId="historical-foreign-product|"+new string('x',230);
        string legacy=SaveCodec.Write(city.State);
        Assert.Equal(legacy,SaveCodec.Write(SaveCodec.Read(legacy).State!));
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
        Assert.Equal(193,FoodDecisionTuning.Capture(new FoodChoiceSettings()).ToWire().Length);
        var settings=new FoodChoiceSettings{FoodMenuMaximumOffersPerShop=8,FoodMenuNewTasteWeight=.5,FoodMenuHealthWeight=2};
        string held=FoodDecisionTuning.Capture(settings).ToWire();settings.FoodMenuMaximumOffersPerShop=1;
        Assert.True(FoodDecisionTuning.TryParse(held,out var replay));Assert.Equal(held,replay!.ToWire());
        Assert.Equal(241,held.Length);
    }

    [Theory]
    [InlineData("{}",4,1,0)]
    [InlineData("{\"FoodMenuMaximumOffersPerShop\":99,\"FoodMenuNewTasteWeight\":\"Infinity\",\"FoodMenuHealthWeight\":-1}",8,2,0)]
    [InlineData("{\"FoodMenuMaximumOffersPerShop\":1.5,\"FoodMenuNewTasteWeight\":{},\"FoodMenuHealthWeight\":\"NaN\"}",4,1,0)]
    [InlineData("{\"FoodMenuMaximumOffersPerShop\":\"1\",\"FoodMenuNewTasteWeight\":\"0.5\",\"FoodMenuHealthWeight\":\"1.5\"}",1,.5,1.5)]
    public void E4_FlatSettingsLoadClampAndReload(string json,int limit,double taste,double health)
    {
        var settings=JsonConvert.DeserializeObject<PrototypeSettings>(json)!;var logs=new List<string>();settings.FoodChoice.Clamp(logs.Add);
        Assert.Equal(limit,settings.FoodMenuMaximumOffersPerShop);Assert.Equal(taste,settings.FoodMenuNewTasteWeight);Assert.Equal(health,settings.FoodMenuHealthWeight);
        Assert.InRange(Math.Min(FoodMenuPolicy.LiveMaximumOffers,settings.FoodMenuMaximumOffersPerShop),1,4);
        int count=logs.Count;settings.FoodChoice.Clamp(logs.Add);Assert.Equal(count,logs.Count);
    }

    [Fact]
    public void E4_CurrentHeaderAndCompactCellsAreStrictBeforeAllocation()
    {
        var (city,_)=S17_Full();
        var row=new FoodShopDay{Day=0,PlaceId="shop",BusinessId="past",Purchases=1,PurchaseMeals=new long[]{1,0,0,0},
            ProductSales={new FoodProductSales{ProductId=FoodProducts.Croissant,Purchases=1,PurchaseMeals=new long[]{1,0,0,0}}},
            BuyerDistricts={new FoodBuyerDistrict{DistrictId="a",Purchases=1}}};
        row.PurchaseHours[0]=1;city.State.Food!.History.Add(row);
        string original=SaveCodec.Write(city.State);
        foreach(string cell in new[]{"[0,1,0,1,0,0,0]","[7,1,0,1,0,0,0]","[1,1,0,1,0,0]","[1,1,0,1,0,0,0,0]","[1,1.0,0,1,0,0,0]","[1,01,0,1,0,0,0]","[1,1,-1,1,0,0,0]","[1,1,0,0,0,0,0]"})
        {
            string bad=original.Replace("\"ProductSales\":[[1,1,0,1,0,0,0]]","\"ProductSales\":["+cell+"]");
            Assert.NotEqual(original,bad);B0_Preserve(bad,city.State);
        }
        var root=JObject.Parse(original);root.Remove("MenuPolicy");
        string missing=root.ToString(Formatting.None);B0_Preserve(missing,city.State);
        Assert.Throws<SaveValidationException>(()=>SaveCodec.RequireExecutableCapabilities(missing));
        root=JObject.Parse(original);root["Food"]!["Shops"]![0]!["AdditionalProductIds"]=new JArray(Enumerable.Range(0,8).Select(i=>"p"+i));
        Assert.Contains("additional menu",SaveCodec.Read(root.ToString(Formatting.None)).Error);
        root=JObject.Parse(original);root["Food"]!["History"]![0]!["ProductSales"]![0]=new JObject{
            ["ProductId"]=FoodProducts.Croissant,["PurchaseMeals"]=new JArray(1,0,0,0),["Purchases"]=1,["Revenue"]=0};
        B0_Preserve(root.ToString(Formatting.None),city.State);
    }
}
