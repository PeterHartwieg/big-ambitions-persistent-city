using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal static class NativeFoodOwnership { internal static long Hour; }
    public sealed partial class PopulationRuntime
    {
        private NativeFoodMarket? foodMarket = null;
    }
    public sealed partial class NativeFoodMarket
    {
        private readonly FoodCommerceState state;
        private readonly Dictionary<string,BuildingRegistration> registrations = new(StringComparer.Ordinal);
        private readonly HashSet<string> candidateBusinesses = new(StringComparer.Ordinal);
        internal NativeFoodMarket(FoodCommerceState state, IEnumerable<BuildingRegistration> native)
        {
            this.state=state;
            foreach(var registration in native)
            {
                registrations.Add("place:"+registration.Id,registration);candidateBusinesses.Add(registration.Id);
                state.Shops.Add(new FoodScope {BusinessId=registration.Id,PlaceId="place:"+registration.Id});
            }
        }
        internal void CandidateForFixture(string business,bool value)
        {
            if(value) candidateBusinesses.Add(business);else candidateBusinesses.Remove(business);
        }
    }
}

namespace UnityEngine
{
    public static class Debug
    {
        public static int CampaignWarnings;
        public static void LogWarning(string message) { CampaignWarnings++; }
    }
}
