using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed class RivalFoodCatalogueRead
    {
        public double AtMinute { get; internal set; }
        public IReadOnlyList<FoodAlternativeObservation> Businesses { get; internal set; } = Array.Empty<FoodAlternativeObservation>();
        public IReadOnlyDictionary<string, FoodCatalogueRefusal> Refused { get; internal set; }
            = new Dictionary<string, FoodCatalogueRefusal>();
    }

    /// <summary>Explicit main-thread diagnostic reader; no scheduler, hook, authority or gameplay
    /// consumer. One instance per loaded city. Never retain this instance across save/reload.
    /// Metadata refresh is bounded; detailed reads touch only the requested (at most eight) shops.</summary>
    public sealed class RivalFoodCatalogue : IDisposable
    {
        public const int MaximumPlaces = 20000;
        public const int DefaultReadsPerUnit = 16;
        private sealed class Entry
        {
            internal string Id = "", Place = "", Street = "", Type = "", Owner = "";
            internal int Number, Day, Seen;
            internal bool Player, Ambiguous;
            internal WeakReference<CityBuildingController> Controller = null!;
            internal WeakReference<BuildingRegistration> Registration = null!;
            // Reset replaces retailPrices even when it reuses the registration object.
            // A mere collection replacement is conservatively ambiguous too.
            internal WeakReference<object>? Prices;
        }
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<(string Street, int Number), Entry> addresses = new Dictionary<(string, int), Entry>();
        private WeakReference<object>? citySave;
        private IEnumerator<CityBuildingController>? pending;
        private int epoch;
        private bool ready, failed;
        public int RetainedPlaces => entries.Count;
        public bool RefreshPending => pending != null;

        public void BeginRefresh()
        {
            var manager = CityManager.Instance;
            if (manager == null || manager.cityBuildingControllers == null || SaveGameManager.Current == null)
                throw new InvalidOperationException("No live city catalogue is available.");
            BeginRefresh(manager.cityBuildingControllers);
        }

        internal void BeginRefresh(IEnumerable<CityBuildingController> controllers)
        {
            if (pending != null || failed) throw new InvalidOperationException("Refresh pending or catalogue refused; dispose at city unload.");
            CheckCity();
            ready = false;
            epoch = checked(epoch + 1);
            pending = controllers.GetEnumerator();
        }

        /// <returns>True only when the complete metadata image is ready. No partial publication.</returns>
        public bool ContinueRefresh(int reads = DefaultReadsPerUnit)
        {
            CheckCity();
            if (reads < 1 || reads > 64) throw new ArgumentOutOfRangeException(nameof(reads));
            if (pending == null) throw new InvalidOperationException("No refresh pending.");
            try
            {
                for (int n = 0; n < reads; n++)
                {
                    if (!pending.MoveNext()) { pending.Dispose(); pending = null; ready = true; return true; }
                    Observe(pending.Current);
                }
                return false;
            }
            catch { pending?.Dispose(); pending = null; failed = true; throw; }
        }

        private void Observe(CityBuildingController controller)
        {
            if (controller == null || controller.building == null) return;
            var reg = controller.buildingRegistration; // Existing field only.
            if (reg == null || string.IsNullOrWhiteSpace(reg.StreetName)) return;
            var address = (reg.StreetName, reg.StreetNumber);
            if (!addresses.TryGetValue(address, out var entry))
            {
                if (entries.Count == MaximumPlaces) throw new InvalidOperationException("Rival catalogue exceeds 20,000 retained places; entire image refused.");
                string place = Place(reg);
                entry = new Entry { Place = place, Street = reg.StreetName, Number = reg.StreetNumber,
                    Day = reg.creationDay, Type = reg.businessTypeName, Owner = Owner(reg), Player = reg.RentedByPlayer,
                    Id = FoodBusinessCatalogue.BusinessIdentity(reg), Controller = new WeakReference<CityBuildingController>(controller),
                    Registration = new WeakReference<BuildingRegistration>(reg),
                    Prices = reg.retailPrices == null ? null : new WeakReference<object>(reg.retailPrices) };
                entries.Add(place, entry);
                addresses.Add(address, entry);
            }
            else if (entry.Seen == epoch || entry.Seen != epoch - 1 || !Matches(entry, controller, reg))
                entry.Ambiguous = true; // Quarantined for this city's diagnostic session; no identity transfer.
            if (!reg.HasEstablishedBusiness || reg.AvailableForRent) entry.Ambiguous = true;
            entry.Seen = epoch;
        }

        private static string Place(BuildingRegistration reg)
        {
            int number = reg.StreetNumber;
            return "address:" + Uri.EscapeDataString(reg.StreetName) + ":" + number.ToString(CultureInfo.InvariantCulture) + ":shop";
        }
        private static string Owner(BuildingRegistration reg) => reg.RentedByPlayer ? "player" : reg.businessOwnerRivalId ?? "";
        private static bool Matches(Entry e, CityBuildingController c, BuildingRegistration r) =>
            e.Controller.TryGetTarget(out var oldController) && ReferenceEquals(c, oldController)
            && e.Registration.TryGetTarget(out var oldRegistration) && ReferenceEquals(r, oldRegistration)
            && e.Street == r.StreetName && e.Number == r.StreetNumber && e.Day == r.creationDay
            && e.Type == r.businessTypeName && e.Owner == Owner(r) && e.Player == r.RentedByPlayer
            && (e.Prices == null ? r.retailPrices == null : e.Prices.TryGetTarget(out var prices) && ReferenceEquals(prices, r.retailPrices));

        /// <summary>On-demand detached dump. At most eight detailed reads, ordered by native
        /// business identity. No cachedAvailableProducts/supply/demand/finance/campaign access.</summary>
        public RivalFoodCatalogueRead ReadSelected(IReadOnlyCollection<string> places)
        {
            if (!ready || failed || pending != null) throw new InvalidOperationException("Complete a successful metadata refresh first.");
            if (places == null || places.Count < 1 || places.Count > 8 || places.Any(string.IsNullOrWhiteSpace)
                || places.Distinct(StringComparer.Ordinal).Count() != places.Count) throw new ArgumentException("Select one to eight distinct shops.");
            CheckCity();
            var save = SaveGameManager.Current;
            if (save == null) throw new InvalidOperationException("No live city catalogue is available.");
            var result = new List<FoodAlternativeObservation>(places.Count);
            var refused = new SortedDictionary<string, FoodCatalogueRefusal>(StringComparer.Ordinal);
            foreach (string place in places)
            {
                if (!entries.TryGetValue(place, out var e) || e.Seen != epoch) { refused.Add(place, FoodCatalogueRefusal.Missing); continue; }
                if (!e.Controller.TryGetTarget(out var c) || c == null || c.building == null)
                { refused.Add(place, FoodCatalogueRefusal.Missing); continue; }
                var r = c.buildingRegistration;
                if (e.Ambiguous || r == null || !Matches(e, c, r))
                { e.Ambiguous = true; refused.Add(place, FoodCatalogueRefusal.AmbiguousIdentity); continue; }
                if (!r.HasEstablishedBusiness || r.AvailableForRent || string.IsNullOrWhiteSpace(r.businessTypeName)
                    || r.businessTypeName == "ba:businesstype_empty" || r.creationDay < 0)
                { refused.Add(place, FoodCatalogueRefusal.Missing); continue; }
                if (e.Owner.Length == 0) { refused.Add(place, FoodCatalogueRefusal.UnknownOwner); continue; }
                var failure = ReadDetails(e, c, r, out var observation);
                if (failure.HasValue) refused.Add(place, failure.Value); else result.Add(observation!);
            }
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            return new RivalFoodCatalogueRead { AtMinute = save.Day * 1440.0 + save.Hour * 60.0 + save.Minute,
                Businesses = result.AsReadOnly(), Refused = new System.Collections.ObjectModel.ReadOnlyDictionary<string, FoodCatalogueRefusal>(refused) };
        }

        private static FoodCatalogueRefusal? ReadDetails(Entry e, CityBuildingController c, BuildingRegistration r,
            out FoodAlternativeObservation? observation)
        {
            observation = null;
            int? rating = r.satisfaction?.customerService;
            if (rating < 0 || rating > 100) return FoodCatalogueRefusal.InvalidRating;
            var offers = new List<FoodAdvertisedOffer>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int unsupported = 0;
            if (r.retailPrices != null) foreach (var price in r.retailPrices)
            {
                if (price == null) continue;
                if (string.IsNullOrWhiteSpace(price.itemName) || !FoodProducts.Known(price.itemName)) { unsupported++; continue; }
                if (!seen.Add(price.itemName) || !Money.TryFromNative(price.price, out var exact)) return FoodCatalogueRefusal.InvalidPrice;
                offers.Add(new FoodAdvertisedOffer(price.itemName, exact));
            }
            var hours = new List<FoodAdvertisedHours>();
            if (r.scheduleDays != null) foreach (var day in r.scheduleDays)
            {
                if (day == null || !day.isOpen || day.openingHourSlots == null) continue;
                int weekday = (int)day.day - 1;
                if (weekday < 0 || weekday > 6) return FoodCatalogueRefusal.InvalidHours;
                foreach (var slot in day.openingHourSlots)
                {
                    if (slot == null) continue;
                    if (slot.startingHour < 0 || slot.startingHour >= slot.endingHour || slot.endingHour > 24)
                        return FoodCatalogueRefusal.InvalidHours;
                    hours.Add(new FoodAdvertisedHours(weekday, slot.startingHour * 60, slot.endingHour * 60));
                }
            }
            bool door = false;
            if (c.entranceDoors != null) foreach (var entrance in c.entranceDoors)
                if (entrance != null && entrance.doorTransform != null) { door = true; break; }
            observation = new FoodAlternativeObservation(e.Id, e.Place, e.Street, e.Number, e.Day, e.Type, e.Owner,
                e.Player ? FoodAlternativeKind.Player : FoodAlternativeKind.Rival, GamePlaces.BuildingAccessible(c),
                door, r.temporarilyClosed, rating, unsupported, offers, hours);
            return null;
        }

        private void CheckCity()
        {
            object? current = SaveGameManager.Current;
            if (current == null || (citySave != null && (!citySave.TryGetTarget(out var previous) || !ReferenceEquals(current, previous))))
            {
                pending?.Dispose(); pending = null; ready = false; failed = true;
                throw new InvalidOperationException("Catalogue city changed; dispose and create a new diagnostic session.");
            }
            if (citySave == null) citySave = new WeakReference<object>(current);
        }

        public void Dispose() { pending?.Dispose(); pending = null; entries.Clear(); addresses.Clear(); citySave = null; ready = false; failed = true; }
    }
}
