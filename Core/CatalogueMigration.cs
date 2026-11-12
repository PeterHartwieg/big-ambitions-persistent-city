using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    public static class CatalogueMigration
    {
        public static void Enrich(CityState state, IEnumerable<Place> discovered)
        {
            var existing = state.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
            var occupied = state.Citizens.GroupBy(c => c.HomeId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var discoveredHomes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var place in discovered)
            {
                if (place.Kind == PlaceKind.Home) discoveredHomes.Add(place.Id);
                if (existing.TryGetValue(place.Id, out var old))
                {
                    old.AttractionWeight = place.AttractionWeight;
                    old.NativeTrafficIndex = place.NativeTrafficIndex;
                    old.EvidenceSource = place.EvidenceSource;
                    if (old.Kind == PlaceKind.Home && place.Kind == PlaceKind.Home)
                    {
                        int residents = occupied.TryGetValue(old.Id, out int count) ? count : 0;
                        old.Capacity = Math.Max(residents, place.Capacity);
                        old.ResidentialAreaSquareMetres = place.ResidentialAreaSquareMetres;
                        if (residents > place.Capacity) old.EvidenceSource += "; existing residents retained";
                    }
                }
                else { state.Places.Add(place); existing.Add(place.Id, place); }
            }
            foreach (var home in state.Places.Where(p => p.Kind == PlaceKind.Home && !discoveredHomes.Contains(p.Id)))
            {
                home.Capacity = occupied.TryGetValue(home.Id, out int count) ? count : 0;
                home.ResidentialAreaSquareMetres = 0;
                home.EvidenceSource = "legacy-home:existing-occupants-only;native-capacity-unavailable";
            }
            state.Places.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            foreach (var person in state.Citizens)
            {
                if (person.LeisureId == "") person.LeisureId = DestinationChoice.Choose(state.Places, existing[person.HomeId],
                    PlaceKind.Leisure, person.Identity.Id + ":leisure", state.Seed);
                if (person.ShopId == "") person.ShopId = DestinationChoice.Choose(state.Places, existing[person.HomeId],
                    PlaceKind.Shop, person.Identity.Id + ":shop", state.Seed);
            }
        }
    }
}
