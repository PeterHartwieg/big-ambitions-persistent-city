using System;
using System.Collections.Generic;
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
{ public Building building = new(); public List<Entrance> entranceDoors = new() { new() }; public bool Accessible = true; public BuildingRegistration buildingRegistration = new(); }
public sealed class Entrance { public object doorTransform = new(); }
public sealed class BuildingRegistration
{
    public void Reset(bool resetFields = true) => throw new Exception("FORBIDDEN reset/writer");
    public void SetupSupply() => throw new Exception("FORBIDDEN supply setup");
    public void RecalculateDemand() => throw new Exception("FORBIDDEN demand recalculation");
    public string StreetName = "Test Street", businessTypeName = "restaurant", businessOwnerRivalId = "rival", BusinessName = "Test Shop";
    public int StreetNumber = 7, creationDay = 1;
    public int StockCanary = 123; public long FinanceCanary = 987654321;
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

public static class CustomerEntriesHelper { public static void GetEntriesByAddress() => throw new Exception("FORBIDDEN customer entries"); }
public static class BuildingHelper { public static void GetBuildingRegistration() => throw new Exception("FORBIDDEN registration creation"); }
