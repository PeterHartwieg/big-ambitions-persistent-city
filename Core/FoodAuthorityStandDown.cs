using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>F5c-lite: food authority for a refused city from an older development build (schemas 1-25). Persistent
    /// City takes no authority and writes nothing: the authority manifest, receipts, the accounting-stop marker and the
    /// save-epoch marker stay byte-identical. A business whose saved manifest owned the loaded hour (or an earlier hour
    /// still draining) stays protected until that hour ends, so a partly mod-served hour is never replayed natively;
    /// from the next native hour every player business is native-owned. Pure: Runtime applies it at its native hooks.</summary>
    public sealed class FoodAuthorityStandDown
    {
        private readonly Dictionary<string, FoodAuthority> rows = new Dictionary<string, FoodAuthority>(StringComparer.Ordinal);
        private bool handedBack;

        public FoodAuthorityStandDown(IEnumerable<FoodAuthority> manifest, long loadedHour)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (loadedHour < 0) throw new ArgumentOutOfRangeException(nameof(loadedHour));
            LoadedHour = loadedHour;
            foreach (var row in manifest)
                if (row != null && row.OwnsHour(loadedHour)) rows[row.PlaceId] = row;
        }

        /// <summary>The native hour in which the refused city was loaded.</summary>
        public long LoadedHour { get; }

        /// <summary>Places whose saved authority owned the loaded hour; they get native customers again at hand-back.</summary>
        public IEnumerable<string> ProtectedPlaces => rows.Keys;

        public bool IsProtected(string placeId) => rows.ContainsKey(placeId);

        /// <summary>True only for a protected place's native work of the loaded hour or an earlier owned hour.</summary>
        public bool Suppresses(string placeId, long hour) => hour <= LoadedHour && rows.TryGetValue(placeId, out var row) && row.OwnsHour(hour);

        /// <summary>True exactly once, at the first native boundary after the loaded hour.</summary>
        public bool HandBack(long hour)
        {
            if (handedBack || hour <= LoadedHour) return false;
            handedBack = true;
            return true;
        }
    }
}
