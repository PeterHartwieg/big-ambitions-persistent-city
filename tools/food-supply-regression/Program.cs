using System;
using System.Collections.Generic;
using System.Linq;
using BigAmbitions.Items;
using Buildings.Retail.Simulation;
using PersistentCity.Runtime;
using UnityEngine;
static class Program
{
    static int checks;
    static void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; }
    static ItemInstance Stock(string product, int units) => new() { ItemCached = new Item { type=ItemType.ShowcaseShelf, itemsThatCanShowcase=new[]{product} }, cargoInstances=new(){new(){itemName=product,amount=units}} };
    static void Main()
    {
        foreach (int empty in new[]{0,1,2})
        {
            var shop = new BuildingRegistration(); var counter = Stock("bag",0);
            if (empty==0) counter.cargoInstances=null; else if(empty==1) counter.cargoInstances!.Clear();
            shop.Counters.Add(new(){instance=counter});shop.Producers["a"]=new(){Stock("a",3)};shop.Producers["b"]=new(){Stock("b",7)};
            int setup=RetailBusinessSimulator.Setups;
            var r=FoodSupplyReader.ReadMenu(shop,new[]{"a","b"});
            Check(r["a"].Units==0 && r["b"].Units==0 && r["a"].MealsPerHour==0,"empty register is known zero supply");
            Check(r["a"].AvailabilityReason=="Out of stock", "empty bags at a staffed register report stock, not staffing");
            Check(r["a"].ProducerUnits==3 && r["b"].ProducerUnits==7,"empty register preserves observed product stocks");
            Check(RetailBusinessSimulator.Setups==setup+1,"one setup per menu");
            shop.Counters.Add(new(){instance=Stock("bag",1)});r=FoodSupplyReader.ReadMenu(shop,new[]{"a","b"});
            Check(r["a"].Units==1 && r["b"].Units==1 && r["a"].BagUnits==1 && r["a"].MealsPerHour==15,"other stocked counter serves the shared pool");
        }
        var unstaffed=new BuildingRegistration(); unstaffed.Producers["a"]=new(){Stock("a",10)};
        var unavailable=FoodSupplyReader.ReadMenu(unstaffed,new[]{"a"})["a"];
        Check(unavailable.StaffedCounters==0 && unavailable.AvailabilityReason=="No staffed counter", "no available native staffed station has its own reason");
        var bad=new BuildingRegistration();bad.Counters.Add(new(){instance=Stock("not-a-bag",2)});
        Check(FoodSupplyReader.ReadMenu(bad,new[]{"a"})["a"].Units==null,"non-bag cargo is unknown");
        var converted=new BuildingRegistration();converted.Counters.Add(new(){instance=Stock("bag",2)});
        converted.Producers["a"]=new(){Stock("resource",4)};converted.Producers["b"]=new(){Stock("b",7)};
        var read=FoodSupplyReader.ReadMenu(converted,new[]{"a","b"});
        Check(read["a"].Units==null && read["b"].Units==2,"converted resource is not inferred as edible units");
        Check(UnityEngine.Object.Destroyed==RetailBusinessSimulator.Setups,"every temporary simulator destroyed");
        Console.WriteLine(checks + " actual FoodSupplyReader checks passed (synthetic native surfaces, not game execution).");
    }
}
public sealed class Save { public int Day=2,Hour=12,Minute; }
public static class SaveGameManager { public static Save Current=new(); }
public sealed class BuildingRegistration
{
 public bool RentedByPlayer=true,HasEstablishedBusiness=true,temporarilyClosed;
 public string businessTypeName="full";
 public Dictionary<string,ItemInstance> itemInstances=new();
 public List<PointOfSale> Counters=new();public Dictionary<string,List<ItemInstance>> Producers=new();
}
namespace UnityEngine
{
 public class Object { public static int Destroyed; public static void Destroy(object obj)=>Destroyed++; }
 public class ScriptableObject:Object { public static T CreateInstance<T>() where T:new()=>new(); }
}
namespace BigAmbitions.Tags { public static class TagRef { public enum Businesstag {customersneedpaperbags} public enum Itemtag {isbag} } }
namespace BigAmbitions.Items
{
 public enum ItemType { ShowcaseShelf=1 }
 public sealed class Cargo { public string itemName="";public int amount; }
 public sealed class ItemInstance { public string id="id"; public Item ItemCached=new();public List<Cargo>? cargoInstances=new(); }
 public sealed class Settings { public bool ResourcesRequired=true; }
 public sealed class Item { public ItemType type;public string[] itemsThatCanShowcase=Array.Empty<string>();public Settings producerSettings=new();public bool Bag;public bool HasTag(BigAmbitions.Tags.TagRef.Itemtag tag)=>Bag; }
 public static class ItemsGetter { public static Item GetByName(string name)=>new(){Bag=name=="bag"}; }
}
namespace Helpers
{
 public sealed class BusinessType { public object simulator=new FullServiceBusinessSimulator();public bool HasTag(BigAmbitions.Tags.TagRef.Businesstag tag)=>true; }
 public static class BusinessTypeHelper { public static BusinessType GetData(string name)=>new(); }
}
namespace Buildings.Retail.Simulation
{
 public sealed class PointOfSale { public ItemInstance instance=new(); }
 public class RetailBusinessSimulator:ScriptableObject
 {
  private Dictionary<string,List<ItemInstance>> availableProducers=new();private List<PointOfSale> pointOfSales=new();
  public static int Setups;public void SetUp(BuildingRegistration reg,int hour){Setups++;availableProducers=reg.Producers;pointOfSales=reg.Counters;}
  private int GetMaxCustomerCapacityThisHour()=>15;
 }
 public sealed class FullServiceBusinessSimulator:RetailBusinessSimulator { }
}
