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
        public NativeFoodMarket(FoodCommerceState food)
        {
            state = food;
            foreach (var scope in food.Shops)
            { candidateBusinesses.Add(scope.BusinessId); registrations.Add(scope.PlaceId, new BuildingRegistration { Id = scope.BusinessId }); }
        }
        public bool ReaderBound => awarenessCampaignReader != null;
        public long RefusedBoundaries => campaignScopeRefusals;
        public long ReadBusinesses => campaignReadBusinesses;
        public IEnumerable<BuildingRegistration> Registrations => registrations.Values;
    }
}
namespace UnityEngine
{
    public static class Debug { public static void LogWarning(string message) => throw new Exception("Unexpected warning: " + message); }
}
