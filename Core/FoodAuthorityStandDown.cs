using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
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

        public static FoodAuthorityStandDown FromManifest(Func<IReadOnlyList<FoodAuthority>> readManifest, long loadedHour, out string? problem)
        {
            if (readManifest == null) throw new ArgumentNullException(nameof(readManifest));
            problem = null;
            try
            {
                var rows = readManifest();
                foreach (var row in rows)
                    if (row == null || row.RequestedHour > loadedHour || row.FromHour > loadedHour || row.UntilHour > loadedHour)
                    { problem = "Food authority is ahead of the loaded native clock"; return new FoodAuthorityStandDown(Array.Empty<FoodAuthority>(), loadedHour); }
                return new FoodAuthorityStandDown(rows, loadedHour);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                problem = "Food authority manifest is unreadable: " + e.Message;
                return new FoodAuthorityStandDown(Array.Empty<FoodAuthority>(), loadedHour);
            }
        }

        public long LoadedHour { get; }

        public IEnumerable<string> ProtectedPlaces => rows.Keys;

        public bool IsProtected(string placeId) => rows.ContainsKey(placeId);

        public bool Suppresses(string placeId, long hour) => hour <= LoadedHour && rows.TryGetValue(placeId, out var row) && row.OwnsHour(hour);

        public bool HandBack(long hour)
        {
            if (handedBack || hour <= LoadedHour) return false;
            handedBack = true;
            return true;
        }
    }
}
