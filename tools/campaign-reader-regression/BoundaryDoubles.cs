using PersistentCity.Core;

// Kept separate from Doubles.cs, which other regression projects explicitly share.
namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private NativeFoodMarket? foodMarket = null;
    }
    public static class NativeFoodOwnership { public static long Hour; }
    public sealed partial class NativeFoodMarket
    {
        private readonly FoodCommerceState state;
        private readonly HashSet<string> candidateBusinesses = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BuildingRegistration> registrations = new(StringComparer.Ordinal);
        private readonly Dictionary<string,FoodScope> scopesByBusiness = new(StringComparer.Ordinal);
        public NativeFoodMarket(FoodCommerceState food)
        {
            state = food;
            foreach (var scope in food.Shops)
            { candidateBusinesses.Add(scope.BusinessId); scopesByBusiness.Add(scope.BusinessId,scope); registrations.Add(scope.PlaceId, new BuildingRegistration { Id = scope.BusinessId }); }
        }
        public bool ReaderBound => awarenessCampaignReader != null;
        public long RefusedBoundaries => campaignScopeRefusals;
        public long ReadBusinesses => campaignReadBusinesses;
        public IEnumerable<BuildingRegistration> Registrations => registrations.Values;
        public void RetainOnlyCandidate(string id) { candidateBusinesses.Clear(); candidateBusinesses.Add(id); }
        public string ObservationError => campaignObservationError;
        public FoodCommerceState Food => state;
        public FoodCampaignSource Source => campaignSource!;
    }
}
namespace UnityEngine
{
    public static class Debug { public static readonly List<string> Warnings = new();
        public static bool ExpectWarning;
        public static void LogWarning(string message)
        { if (!ExpectWarning) throw new Exception("Unexpected warning: " + message); Warnings.Add(message); } }
}
