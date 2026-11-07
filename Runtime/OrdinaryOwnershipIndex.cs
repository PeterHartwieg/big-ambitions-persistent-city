using System;
using System.Collections.Generic;
using System.Diagnostics;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>Main-thread hourly observation, 16 reads then one atomic unit publication. No native writes.</summary>
    public sealed class OrdinaryOwnershipIndex : IOrdinaryVisitOwnership
    {
        public const int PlacesPerUnit = 16, MaximumPlaces = 20000;
        private readonly Place[] places;
        private readonly OrdinaryVisitTerms[] published;
        private readonly OrdinaryVisitTerms[] staged = new OrdinaryVisitTerms[PlacesPerUnit];
        private readonly Dictionary<string, int> byId;
        private readonly Func<Place, OrdinaryVisitTerms> read;
        private long hour;
        private int cursor;
        private bool active;
        public OrdinaryOwnershipIndex(IReadOnlyList<Place> places, double loadedMinute, Func<Place, OrdinaryVisitTerms> read)
        {
            if (places.Count > MaximumPlaces) throw new ArgumentOutOfRangeException(nameof(places));
            this.read = read; hour = (long)(loadedMinute / 60); cursor = places.Count;
            this.places = new Place[places.Count]; published = new OrdinaryVisitTerms[places.Count];
            byId = new Dictionary<string, int>(places.Count, StringComparer.Ordinal);
            for (int i = 0; i < places.Count; i++)
            {
                this.places[i] = places[i]; byId.Add(places[i].Id, i);
                published[i] = new OrdinaryVisitTerms(-1, false, OrdinaryVisitClass.ActivityOnly);
            }
        }
        public bool Pending => cursor < places.Length;
        public int Places => places.Length;
        public long PublishedHour => active ? hour : -1;
        public double LastUnitMilliseconds { get; private set; }
        public double MaximumUnitMilliseconds { get; private set; }
        public long CompletedUnits { get; private set; }
        public long NativeReads { get; private set; }
        public int LastUnitPlaces { get; private set; }
        public void ObserveBoundary(double minute)
        {
            long next = (long)(minute / 60);
            if (next <= hour) return;
            if (Pending) throw new InvalidOperationException("Ownership observation must finish inside its epoch.");
            hour = next; cursor = 0; active = true;
        }
        public void Continue()
        {
            long started = Stopwatch.GetTimestamp();
            int count = Math.Min(PlacesPerUnit, places.Length - cursor);
            LastUnitPlaces = count;
            for (int i = 0; i < count; i++)
            {
                NativeReads++;
                var facts = read(places[cursor + i]);
                staged[i] = new OrdinaryVisitTerms(hour, facts.PlayerOwned, facts.Kind);
            }
            // A failed read publishes none of this unit; all its older entries fail the hour guard.
            for (int i = 0; i < count; i++) published[cursor + i] = staged[i];
            cursor += count; CompletedUnits++;
            LastUnitMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            MaximumUnitMilliseconds = Math.Max(MaximumUnitMilliseconds, LastUnitMilliseconds);
        }
        public bool TryGet(string placeId, out OrdinaryVisitTerms terms)
        {
            if (active && byId.TryGetValue(placeId, out int at)) { terms = published[at]; return terms.Hour == hour; }
            terms = default; return false;
        }
    }
}
