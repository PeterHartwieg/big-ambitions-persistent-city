// Shapes backed separately by the DLL findings; these doubles are NOT evidence of a native API.
public sealed class Address { public string streetName = "Broadway"; public int streetNumber = 1; }
public sealed class Building
{
    public string StreetName = "Broadway"; public int StreetNumber = 1;
    public Address Address => throw new Exception("FORBIDDEN lazy native cache write");
    public Buildings.SpecialService? SpecialService;
}
public sealed class CityBuildingController { public Building building = new(); }
public sealed class CityManager
{
    public static CityManager? Registered;
    public static CityManager Instance => throw new Exception("FORBIDDEN native singleton registration/search");
    public CityBuildingController[] cityBuildingControllers = Array.Empty<CityBuildingController>();
}
public sealed class GameInstance
{
    public int Day = 9; public float Money = 1000.125f;
    public Queue<Transaction> Transactions = new(); public List<Summary> financialSummaries = new();
}
public sealed class SaveGameManager { public static GameInstance? Current; }
public sealed class DialogController { public static DialogController? current; public string dialogType = "PhoneCall"; public Contact? contact; }
public sealed class Contact { public string streetName = "Broadway", id = "agency"; public int streetNumber = 1; }
public sealed class Timestamp { public int Day = 8, Hour = 23; public float Minute = 59.5f; }
public sealed class Transaction
{
    public string transactionType = "ba:transaction_marketing";
    public List<string> transactionCategories = new() { "ba:transactioncategory_marketing" };
    public float amount = -100.125f, balance = 899; public Timestamp timestamp = new(); public Address address = new();
    public Dictionary<string, string> transactionData = new() { ["businessName"] = "Fixture" };
}
public sealed class Summary { public int dayNumber = 8; public List<Statement> businessIncomeStatements = new(); }
public sealed class Statement { public Address Address = new(); public float MarketingExpenses = 100.125f; }
namespace Buildings
{
    public sealed class MarketingAgencySettings { public List<string> marketingTypesAvailable = new() { "SmallInternet", "SmallBillboard" }; }
    public sealed class SpecialService { public object? settings; public string businessTypeName = "fixture-native-type", dialogType = "MarketingAgency"; }
}
namespace Extensions
{
    public static class SingletonLifecycle
    {
        public static T? GetSingleton<T>() where T : class => CityManager.Registered as T;
    }
}
