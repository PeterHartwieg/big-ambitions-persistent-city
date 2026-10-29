using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    // Native references stay transient. The caller supplies a fresh live image before every reuse.
    internal sealed class AudienceTarget
    {
        public AudienceTarget(object zone, object performer, string id, Position position)
        { Zone = zone; Performer = performer; Id = id; Position = position; }
        public object Zone { get; }
        public object Performer { get; }
        public string Id { get; }
        public Position Position { get; }
        public bool Matches(AudienceTarget other) => ReferenceEquals(Zone, other.Zone)
            && ReferenceEquals(Performer, other.Performer) && Id == other.Id && Position.Equals(other.Position);
    }

    internal sealed class AudienceGeometryCache
    {
        private sealed class Entry
        {
            public Position Origin;
            public AudienceTarget[] Targets = Array.Empty<AudienceTarget>();
            public IReadOnlyList<OptionalStopSpot> Spots = Array.Empty<OptionalStopSpot>();
            public bool Matches(Position origin, IReadOnlyList<AudienceTarget> targets)
            {
                if (!Origin.Equals(origin) || Targets.Length != targets.Count) return false;
                for (int i = 0; i < Targets.Length; i++) if (!Targets[i].Matches(targets[i])) return false;
                return true;
            }
        }
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        public IReadOnlyList<OptionalStopSpot> Get(Place place, IReadOnlyList<AudienceTarget> targets,
            Func<Place, IReadOnlyList<AudienceTarget>, IReadOnlyList<OptionalStopSpot>> sample)
        {
            if (entries.TryGetValue(place.Id, out var entry) && entry.Matches(place.Position, targets)) return entry.Spots;
            var image = new AudienceTarget[targets.Count];
            for (int i = 0; i < image.Length; i++) image[i] = targets[i];
            var result = sample(place, image);
            entries[place.Id] = new Entry { Origin = place.Position, Targets = image, Spots = result };
            return result;
        }
    }
}
