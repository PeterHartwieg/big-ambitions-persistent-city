using PersistentCity.Core;

namespace Entities
{
    public enum MarketingTypeName { SmallInternet, MediumInternet, LargeInternet, SmallBillboard, MediumBillboard, LargeBillboard, None }
    public sealed class MarketingCampaign
    {
        public MarketingTypeName marketingTypeName;
        public bool enabled = true;
        public Address agencyAddress = new Address();
    }
}
public sealed class Address { public string streetName = "agency"; public int streetNumber = 7; }
public sealed class Promotion { public int trafficIndex = 20; public int marketing = 30; public int total = 50; }
public sealed class BuildingRegistration
{
    public string Id = "business:fixture";
    public List<Entities.MarketingCampaign> marketingCampaigns = Enumerable.Range(0, 6)
        .Select(i => new Entities.MarketingCampaign { marketingTypeName = (Entities.MarketingTypeName)i }).ToList();
    public Promotion promotion = new Promotion();
    public int MarketingExpenses = 170;
    public int NativeCustomers = 19;
    [System.Text.Json.Serialization.JsonIgnore]
    public int Money { get => throw new Exception("Money getter"); set => throw new Exception("Money setter"); }
    public void GetDailyMarketingExpenses() => throw new Exception("Native helper");
    public void UpdatePromotion() => throw new Exception("Native writer");
}
public sealed class Save
{
    public List<BuildingRegistration> BuildingRegistrations = new List<BuildingRegistration> { new BuildingRegistration() };
    public int Day = 1, Hour = 2, Minute = 3;
}
public static class SaveGameManager { public static Save? Current = new Save(); }
namespace PersistentCity.Runtime
{
    public static class FoodBusinessCatalogue { internal static string BusinessIdentity(BuildingRegistration reg) => reg.Id; }
    public sealed partial class PopulationRuntime
    {
        private bool failed = false;
        private object? saveIdentity = SaveGameManager.Current;
        private object? simulation = new object();
    }
}
