using System.Collections.Generic;

namespace PersistentCity.Persistence
{
    /// <summary>Bounded failure intent, separate from the unchanged schema-25 population.
    /// Presence (including an unknown value) prevents automatic restart over archived state.</summary>
    public static class AccountingStopStore
    {
        public const string Key = "persistentcity:accounting-stop_v1";
        private const string InputEpochLost = "input-epoch-lost";
        /// <summary>F7b diagnostic values; every build refuses on presence alone, whatever the value.</summary>
        public const string SaveEpochFailed = "save-epoch-failed";
        public const string DetachedAfterSave = "detached-after-save";
        public static bool IsStopped(IDictionary<string, string> data) => data.ContainsKey(Key);
        public static void Stop(IDictionary<string, string> data) => Stop(data, InputEpochLost);
        /// <summary>The first recorded intent wins; later stops never rewrite it.</summary>
        public static void Stop(IDictionary<string, string> data, string value)
        {
            if (!data.ContainsKey(Key)) data.Add(Key, value);
        }
    }
}
