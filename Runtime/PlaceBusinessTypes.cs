using System;
using System.Collections.Generic;
using System.Diagnostics;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private readonly BusinessTypeIndex businessTypes = new BusinessTypeIndex();
        private readonly Dictionary<string, string> liveBusinessTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        private long businessTypeHour = long.MinValue;
        private double maxBusinessTypeMilliseconds;

        private void RefreshBusinessTypes(IReadOnlyList<Place> places, double minute, string reason)
        {
            long hour = (long)Math.Floor(minute / 60);
            if (reason == "hour" && hour == businessTypeHour) return;
            businessTypeHour = hour;
            long started = Stopwatch.GetTimestamp();
            var changes = BusinessTypeRefresh.Apply(places, GamePlaces.ReadBusinessTypes(liveBusinessTypes));
            if (changes.Any || reason != "hour") businessTypes.Rebuild(places);
            double milliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            maxBusinessTypeMilliseconds = Math.Max(maxBusinessTypeMilliseconds, milliseconds);
            if (changes.Any || reason != "hour" || milliseconds >= 5)
                log?.Info("Business types (" + reason + "): " + changes + "; types=" + businessTypes.TypeCount
                    + " live_addresses=" + liveBusinessTypes.Count + " ms=" + milliseconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    + " max_ms=" + maxBusinessTypeMilliseconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".");
        }
    }
}
