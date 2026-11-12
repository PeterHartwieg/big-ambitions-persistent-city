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
        /// <summary>The load-time refusal cause for a stopped save, by its recorded intent. Diagnostic text only: every
        /// value, unknown ones included, refuses the same way.</summary>
        public static string Cause(IDictionary<string, string> data)
        {
            data.TryGetValue(Key, out var value);
            switch (value)
            {
                case SaveEpochFailed:
                    return "This save did not publish a coherent Persistent City snapshot (for example it was written while a Persistent City "
                        + "sale or event was running), so its native books may hold a sale its residents never paid for. Persistent City "
                        + "never settles or replays such a sale; the earlier coherent save holds it undone.";
                case DetachedAfterSave:
                    return "Persistent City had already stopped when this save was written, so its population is older than its native books.";
                default:
                    return "Accounting input epoch was lost.";
            }
        }
        public static void Stop(IDictionary<string, string> data) => Stop(data, InputEpochLost);
        /// <summary>The first recorded intent wins; later stops never rewrite it.</summary>
        public static void Stop(IDictionary<string, string> data, string value)
        {
            if (!data.ContainsKey(Key)) data.Add(Key, value);
        }
    }
}
