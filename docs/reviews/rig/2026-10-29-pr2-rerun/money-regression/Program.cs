using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using PersistentCity.Core;
using PersistentCity.Runtime;
using PersistentCity.Persistence;
object Get(object obj,string n)=> (obj.GetType().GetProperty(n)??obj.GetType().GetProperty(n+"Cents"))!.GetValue(obj)!;
BigInteger Units(object value) {var p=value.GetType().GetProperty("Units");return p!=null?new BigInteger((long)p.GetValue(value)!):new BigInteger(Convert.ToInt64(value))*268435456;}
void Set(object obj,string n,long cents) {var p=obj.GetType().GetProperty(n)??obj.GetType().GetProperty(n+"Cents");var v=p!.PropertyType==typeof(long)?(object)cents:p.PropertyType==typeof(int)?(object)(int)cents:p.PropertyType.GetMethod("FromCents")!.Invoke(null,new object[]{cents})!;p.SetValue(obj,v);}
const float native=10.2528f;
var reg=new BuildingRegistration();reg.retailPrices[0].price=native;
CityManager.Instance.cityBuildingControllers.Add(new CityBuildingController{buildingRegistration=reg});
var offer=FoodBusinessCatalogue.Read(new[]{"address:Test%20Street:7:shop"},new[]{"food"}).Businesses.Single().Offers.Single();
var price=Get(offer,"Price");var budget=new ResidentBudget();Set(budget,"OpeningBalance",200000);Set(budget,"Balance",200000);
var hold=typeof(HouseholdFinance).GetMethod("HoldPurchase")!;
for(int i=0;i<109;i++){if(!(bool)hold.Invoke(null,new[]{(object)budget,price})!)throw new Exception("hold refused");HouseholdFinance.ConfirmPurchase(budget);}
BigInteger expected=275221504000; // 10.25279998779296875 dollars * 100 * 2^28
bool fractional=Units(price)==expected && Units(Get(budget,"PreparedFoodSpent"))==expected*109 && Units(Get(budget,"HeldPurchase"))==0;
Console.WriteLine($"nativeFloatBits={BitConverter.SingleToInt32Bits(native)}; chargeUnits={Units(price)}; exactExpectedUnits={expected}; debit109Units={Units(Get(budget,"PreparedFoodSpent"))}; expected109Units={expected*109}; heldUnits={Units(Get(budget,"HeldPurchase"))}; fractional={(fractional?"PASS":"FAIL")}");
string legacy=System.IO.File.ReadAllText(args[0]);var read=SaveCodec.Read(legacy);if(read.Status!=SaveReadStatus.Loaded)throw new Exception(read.Error);
string current=SaveCodec.Write(read.State!);
int rejected=0,count=0;
foreach(var row in new[]{("schema13",legacy),("current",current)}){
 foreach(string root in new[]{"Allowance","Arrears"}){
 var match=Regex.Match(row.Item2,"\""+root+"(?:Cents)?\":([0-9]+)");if(!match.Success)throw new Exception("missing "+root);
 long value=long.Parse(match.Groups[1].Value);string damaged=row.Item2.Substring(0,match.Groups[1].Index)+"0x"+value.ToString("X")+row.Item2.Substring(match.Groups[1].Index+match.Groups[1].Length);
 var result=SaveCodec.Read(damaged);bool refused=result.Status==SaveReadStatus.Invalid;if(refused)rejected++;count++;
 Console.WriteLine($"moneyToken generation={row.Item1} field={root} hex={value:X} expected=Invalid observed={result.Status} {(refused?"PASS":"FAIL")}");
 }}
Console.WriteLine($"money-token rejection {rejected}/{count}; fractional {(fractional?"PASS":"FAIL")}");
Environment.ExitCode=fractional && rejected==count?0:1;
public sealed class Save
{
    public int Day = 1, Hour = 12; public float Minute;
    public List<ProductMarketEntry>? productMarketEntries = new() { new() };
}
public sealed class ProductMarketEntry { public string itemName = "food"; public List<NeighborhoodDemand> demandValues = new() { new() }; }
public sealed class NeighborhoodDemand { public string neighborhood = "district-a"; public int demand = 80; }
public sealed class Satisfaction { public int customerService = 75; }
public sealed class Building { public string Neighbourhood = "district-a"; }
public static class SaveGameManager { public static Save Current = new(); }
public sealed class CityManager
{ public static CityManager Instance = new(); public List<CityBuildingController> cityBuildingControllers = new(); }
public sealed class CityBuildingController
{ public Building building = new(); public bool Accessible = true; public BuildingRegistration buildingRegistration = new(); }
public sealed class BuildingRegistration
{
    public string StreetName = "Test Street", businessTypeName = "restaurant", businessOwnerRivalId = "rival", BusinessName = "Test Shop";
    public int StreetNumber = 7, creationDay = 1;
    public Satisfaction? satisfaction = new();
    public bool RentedByPlayer, AvailableForRent, temporarilyClosed;
    public bool HasEstablishedBusiness => BusinessName.Length > 0;
    public List<RetailPrice> retailPrices = new() { new() };
    public List<string> cachedAvailableProducts = new() { "food" };
    public List<ScheduleDay> scheduleDays = new() { new() };
}
public sealed class RetailPrice { public string itemName = "food"; public float price = 5.25f; }
public sealed class ScheduleDay
{ public bool isOpen = true; public int day = 1; public List<OpeningSlot> openingHourSlots = new() { new() }; }
public sealed class OpeningSlot { public int startingHour = 10, endingHour = 20; }
namespace PersistentCity.Runtime
{ internal static class GamePlaces { public static int Checks; public static bool BuildingAccessible(CityBuildingController c) {Checks++;return c.Accessible;} } }
