using System.Collections.Generic;

namespace PersistentCity.Persistence
{
    /// <summary>Bounded failure intent, separate from the unchanged schema-25 population.
    /// Presence (including an unknown value) prevents automatic restart over archived state.</summary>
    public static class AccountingStopStore
    {
        public const string Key = "persistentcity:accounting-stop_v1";
        private const string InputEpochLost = "input-epoch-lost";
        public static bool IsStopped(IDictionary<string, string> data) => data.ContainsKey(Key);
        public static void Stop(IDictionary<string, string> data)
        {
            if (!data.ContainsKey(Key)) data.Add(Key, InputEpochLost);
        }
    }
}
