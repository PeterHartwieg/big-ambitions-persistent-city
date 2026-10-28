using PersistentCity.Runtime;

int checks = 0;
void Check(bool value, string description) { checks++; if (!value) throw new Exception(description); }
void Refuses(Action call, string description)
{ try { call(); } catch (ArgumentException) { checks++; return; } catch (InvalidOperationException) { checks++; return; } throw new Exception(description); }
const string place = "address:Test%20Street:7:shop";
var reg = new BuildingRegistration();
var controller = new CityBuildingController { buildingRegistration = reg };
CityManager.Instance.cityBuildingControllers.Add(controller);
var first = FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" });
Check(first.Businesses.Count == 1 && first.AtMinute == 2160, "reads one selected shop at the game clock");
var business = first.Businesses.Single();
Check(business.PlaceId == place && business.Offers.Single().Price == PersistentCity.Core.Money.FromCents(525), "copies address and price");
Check(business.Offers.Single().Units == null, "advertised food does not invent stock");
Check(business.Hours.Single().Day == 0, "native Monday becomes Core Monday");
Check(reg.retailPrices.Single().price == 5.25f && reg.scheduleDays.Single().openingHourSlots.Single().endingHour == 20, "input retained");
business.Offers[0].Price = PersistentCity.Core.Money.FromCents(2); business.Hours.Clear();
Check(reg.retailPrices[0].price == 5.25f && reg.scheduleDays.Count == 1, "snapshot edits are detached");
reg.retailPrices[0].price = 7.30f;
var next = FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" });
Check(next.Businesses[0].Offers[0].Price == PersistentCity.Core.Money.FromNative(7.30f) && next.Businesses[0].Id == business.Id, "fresh read sees price without changing business identity");
for(int n=0;n<1000;n++) CityManager.Instance.cityBuildingControllers.Add(new CityBuildingController {buildingRegistration=new BuildingRegistration {StreetNumber=100+n}});
var ninePlaces = CityManager.Instance.cityBuildingControllers.Take(9).Select(c => "address:Test%20Street:" + c.buildingRegistration.StreetNumber + ":shop").ToArray();
Check(FoodBusinessCatalogue.Read(ninePlaces, new[] { "food" }).Businesses.Count == 9, "authority observation does not silently drop shops beyond the old eight-shop limit");
var generated = FoodBusinessCatalogue.ReadSelected(new[] { place }, new[] { "food" }, new[] { controller }, r => "reviewed-generation:42");
Check(generated.Businesses.Single().Id == "reviewed-generation:42", "live catalogue uses the authority's exact business generation without rewriting prices");
GamePlaces.Checks=0;
var bound=FoodBusinessCatalogue.ReadSelected(new[]{place},new[]{"food"},new[]{controller});
Check(GamePlaces.Checks==1 && bound.Businesses.Single().Id==next.Businesses.Single().Id,"bound read checks only the enrolled controller among 1001 buildings");
controller.Accessible=false;
Check(FoodBusinessCatalogue.ReadSelected(new[]{place},new[]{"food"},new[]{controller}).Businesses.Count==0,"bound read still rechecks live access");
controller.Accessible=true;
reg.creationDay++;
Check(FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }).Businesses[0].Id != business.Id, "new recorded business generation changes identity");
reg.temporarilyClosed = true;
Check(FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }).Businesses[0].Hours.Count == 0, "closed business remains observed and closed");
reg.temporarilyClosed = false;
reg.AvailableForRent = true;
var vacant = FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" });
Check(vacant.Businesses.Count == 0 && vacant.UnavailablePlaces.Single() == place, "vacant unit is not an operating business");
reg.AvailableForRent = false;
controller.Accessible = false;
Check(FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }).Businesses.Count == 0, "inaccessible building is excluded");
controller.Accessible = true;
Check(FoodBusinessCatalogue.Read(new[] { place }, new[] { "not-food" }).Businesses[0].Offers.Count == 0, "reviewed product list gates offers");
Check(next.Businesses[0].CustomerServicePercent == 75 && next.Businesses[0].DistrictId == "district-a", "saved quality and district observed");
Check(next.Businesses[0].Offers[0].DistrictDemandPercent == 80, "saved district demand observed for the product");
reg.satisfaction!.customerService = 35;
SaveGameManager.Current.productMarketEntries![0].demandValues[0].demand = 25;
var refreshed = FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }).Businesses[0];
Check(refreshed.CustomerServicePercent == 35 && refreshed.Offers[0].DistrictDemandPercent == 25, "refresh sees changed signals");
Check(next.Businesses[0].CustomerServicePercent == 75 && next.Businesses[0].Offers[0].DistrictDemandPercent == 80, "older snapshot remains detached");
SaveGameManager.Current.productMarketEntries[0].demandValues.Add(new NeighborhoodDemand { neighborhood = "other", demand = 99 });
Check(FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }).Businesses[0].Offers[0].DistrictDemandPercent == 25, "other district demand does not leak into local product");
SaveGameManager.Current.productMarketEntries[0].demandValues[0].demand = -10;
Check(FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }).Businesses[0].Offers[0].DistrictDemandPercent == 0, "negative native event demand normalized without mutating source");
Check(SaveGameManager.Current.productMarketEntries[0].demandValues[0].demand == -10, "native demand unchanged");
SaveGameManager.Current.productMarketEntries[0].demandValues.Add(new NeighborhoodDemand());
Refuses(() => FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }), "ambiguous district demand refused");
SaveGameManager.Current.productMarketEntries = null; reg.satisfaction = null;
var missingSignals = FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }).Businesses[0];
Check(missingSignals.CustomerServicePercent == null && missingSignals.Offers[0].DistrictDemandPercent == null, "missing signals stay unknown rather than inventing 50 or 100");
reg.satisfaction = new Satisfaction { customerService = 101 };
Refuses(() => FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }), "invalid service score refused");
reg.satisfaction = null;
reg.retailPrices.Add(new RetailPrice());
Refuses(() => FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }), "duplicate product prices refused");
reg.retailPrices.RemoveAt(1); reg.retailPrices[0].price = float.NaN;
Refuses(() => FoodBusinessCatalogue.Read(new[] { place }, new[] { "food" }), "invalid native price refused");
Refuses(() => FoodBusinessCatalogue.Read(Array.Empty<string>(), new[] { "food" }), "explicit bounded selection required");
Console.WriteLine($"{checks} actual catalogue-adapter checks passed against native field doubles. No Unity/game execution.");

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
