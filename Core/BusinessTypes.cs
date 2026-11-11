using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>GR-1: what the city knows about one business type name. A name no row describes, a modded type included,
    /// gets <see cref="BusinessTypes.Neutral"/>.</summary>
    public sealed class BusinessTypeProfile
    {
        internal BusinessTypeProfile(string type, string essentialCategory)
        { Type = type; EssentialCategory = essentialCategory; }
        /// <summary>The catalogue name this row describes; empty for the neutral profile.</summary>
        public string Type { get; }
        /// <summary>The <see cref="HouseholdEssentials"/> category this type sells, or empty when it sells none.</summary>
        public string EssentialCategory { get; }
        /// <summary>J0 need model defaults: interval and spread in days. Every row uses the neutral numbers until GR-3 tunes them.</summary>
        public int NeedIntervalDays => BusinessTypes.NeutralIntervalDays;
        public int NeedSpreadDays => BusinessTypes.NeutralSpreadDays;
        public bool IsNeutral => Type.Length == 0;
    }

    public static class BusinessTypes
    {
        public const string None = "";
        public const string Empty = "ba:businesstype_empty";
        public const int MaximumNameLength = 256;
        public const int NeutralIntervalDays = 7, NeutralSpreadDays = 3;
        public static readonly BusinessTypeProfile Neutral = new BusinessTypeProfile("", "");

        // Only names confirmed in this repository's evidence; any other name is neutral.
        private static readonly Dictionary<string, BusinessTypeProfile> Rows = Table(
            ("ba:businesstype_clothingstore", HouseholdEssentials.Clothing),
            ("ba:businesstype_furniturestore", HouseholdEssentials.HouseholdGoods),
            ("ba:businesstype_appliancestore", HouseholdEssentials.HouseholdGoods),
            ("ba:businesstype_electronicsstore", HouseholdEssentials.HouseholdGoods));

        private static Dictionary<string, BusinessTypeProfile> Table(params (string Type, string Category)[] rows)
        {
            var table = new Dictionary<string, BusinessTypeProfile>(StringComparer.Ordinal);
            foreach (var (type, category) in rows) table.Add(type, new BusinessTypeProfile(type, category));
            return table;
        }

        public static BusinessTypeProfile For(string? type) => type != null && Rows.TryGetValue(type, out var row) ? row : Neutral;

        /// <summary>A live registration's type name as a place stores it: empty for none, blank and the native empty type.</summary>
        public static string Normalize(string? name) => string.IsNullOrWhiteSpace(name) || name == Empty ? None : name!;

        /// <summary>The address part of a GamePlaces ID ("address:&lt;street&gt;:&lt;number&gt;"), or null for a place
        /// without one (outdoor spots, portals, vendor stands).</summary>
        public static string? AddressKey(string placeId)
        {
            if (placeId == null || !placeId.StartsWith("address:", StringComparison.Ordinal)) return null;
            int kind = placeId.LastIndexOf(':');
            return kind <= "address:".Length ? null : placeId.Substring(0, kind);
        }
    }

    public readonly struct BusinessTypeChanges
    {
        public BusinessTypeChanges(int opened, int closed, int changed) { Opened = opened; Closed = closed; Changed = changed; }
        /// <summary>Places that had no type and now have one.</summary>
        public int Opened { get; }
        /// <summary>Places that had a type and now have none.</summary>
        public int Closed { get; }
        /// <summary>Places whose type was replaced by another.</summary>
        public int Changed { get; }
        public bool Any => Opened + Closed + Changed > 0;
        public override string ToString() => "opened=" + Opened + " closed=" + Closed + " changed=" + Changed;
    }

    /// <summary>GR-1: brings each saved place's type up to date with the live catalogue. Place IDs never change; only
    /// <see cref="Place.BusinessType"/> does. Homes keep none. A business address missing from the live read has no
    /// established business now and returns to none.</summary>
    public static class BusinessTypeRefresh
    {
        /// <param name="live">Address key (<see cref="BusinessTypes.AddressKey"/>) to the normalized type of every
        /// address with an established business now.</param>
        public static BusinessTypeChanges Apply(IReadOnlyList<Place> places, IReadOnlyDictionary<string, string> live)
        {
            if (places == null || live == null) throw new ArgumentNullException(places == null ? nameof(places) : nameof(live));
            int opened = 0, closed = 0, changed = 0;
            foreach (var place in places)
            {
                if (place.Kind == PlaceKind.Home) continue;
                string? key = BusinessTypes.AddressKey(place.Id);
                if (key == null) continue;
                string now = live.TryGetValue(key, out var type) ? BusinessTypes.Normalize(type) : BusinessTypes.None;
                if (now.Length > BusinessTypes.MaximumNameLength) now = BusinessTypes.None;
                string before = place.BusinessType ?? BusinessTypes.None;
                if (string.Equals(before, now, StringComparison.Ordinal)) continue;
                if (before.Length == 0) opened++; else if (now.Length == 0) closed++; else changed++;
                place.BusinessType = now;
            }
            return new BusinessTypeChanges(opened, closed, changed);
        }
    }

    /// <summary>GR-1: transient lookup from type to places, rebuilt from <see cref="Place.BusinessType"/> after every
    /// refresh. It holds place IDs only, so a closed business leaves nothing behind once the index is rebuilt.</summary>
    public sealed class BusinessTypeIndex
    {
        private static readonly IReadOnlyList<string> NoPlaces = Array.Empty<string>();
        private readonly Dictionary<string, List<string>> byType = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public int TypeCount => byType.Count;
        public IEnumerable<string> Types => byType.Keys;

        public void Rebuild(IReadOnlyList<Place> places)
        {
            byType.Clear();
            foreach (var place in places)
            {
                string type = place.BusinessType ?? "";
                if (type.Length == 0) continue;
                if (!byType.TryGetValue(type, out var list)) byType.Add(type, list = new List<string>());
                list.Add(place.Id);
            }
            foreach (var list in byType.Values) list.Sort(StringComparer.Ordinal);
        }

        /// <summary>Place IDs of this type in ordinal order; empty for a type no place has.</summary>
        public IReadOnlyList<string> PlacesOf(string type) => type != null && byType.TryGetValue(type, out var list) ? list : NoPlaces;
    }
}
