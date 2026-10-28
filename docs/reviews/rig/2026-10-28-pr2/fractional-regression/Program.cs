using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
const float native=10.2528f;
var reg=new BuildingRegistration(); reg.retailPrices[0].price=native;
CityManager.Instance.cityBuildingControllers.Add(new CityBuildingController {buildingRegistration=reg});
var offer=FoodBusinessCatalogue.Read(new[]{"address:Test%20Street:7:shop"},new[]{"food"}).Businesses.Single().Offers.Single();
decimal cents=Convert.ToDecimal(offer.PriceCents);
var budget=new ResidentBudget {OpeningBalanceCents=200000,BalanceCents=200000};
var hold=typeof(HouseholdFinance).GetMethod("HoldPurchase")!;
for(int i=0;i<109;i++) {
 object amount=Convert.ChangeType(offer.PriceCents,hold.GetParameters()[1].ParameterType);
 if(!(bool)hold.Invoke(null,new[]{(object)budget,amount})!)throw new Exception("hold refused");
 HouseholdFinance.ConfirmPurchase(budget);
}
Console.WriteLine($"nativeFloatBits={BitConverter.SingleToInt32Bits(native)}; chargeCents={cents}; debitCents={budget.PreparedFoodSpentCents}; heldCents={budget.HeldPurchaseCents}");
bool pass=cents==1025.279998779297m && Convert.ToDecimal(budget.PreparedFoodSpentCents)==111755.519866943373m && budget.HeldPurchaseCents==0;
Console.WriteLine(pass?"PASS: PR2 fractional-charge and 109-sale wallet regression":"FAIL: PR2 fractional-charge and 109-sale wallet regression");
Environment.ExitCode=pass?0:1;
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
