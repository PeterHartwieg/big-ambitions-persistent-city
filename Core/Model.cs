using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum Persona { Commuter, Foodie, Parent, Student, Retiree, Sporty, Grumpy, NightOwl }
    public enum PlaceKind { Home, Work, Shop, Leisure, Study }
    public enum Activity { Home, Work, Shop, Leisure, Study }
    public enum TravelMode { Walk, Bus, Subway, Car }
    public enum CitizenPhase { AtPlace, Travelling, WaitingForRoute, WaitingForOpening }

    public readonly struct Position
    {
        public Position(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static double Distance(Position a, Position b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z));
        public static Position Lerp(Position a, Position b, double t) => new Position(a.X+(b.X-a.X)*t, a.Y+(b.Y-a.Y)*t, a.Z+(b.Z-a.Z)*t);
    }

    public sealed class CitizenIdentity
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string District { get; set; } = "";
        public int Age { get; set; }
        public bool Female { get; set; }
        public Persona Persona { get; set; }
        public int AppearanceSeed { get; set; }
    }

    public sealed class Place
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string District { get; set; } = "";
        public PlaceKind Kind { get; set; }
        public Position Position { get; set; }
        public int Capacity { get; set; } = 1;
        public int OpenMinute { get; set; }
        public int CloseMinute { get; set; } = 1440;
        public List<OpeningWindow> WeeklyHours { get; set; } = new List<OpeningWindow>();
        public double AttractionWeight { get; set; } = 1;
        public int NativeTrafficIndex { get; set; } = -1;
        public string EvidenceSource { get; set; } = "legacy";
        public double ResidentialAreaSquareMetres { get; set; }
        // Transient and never serialized: DailyRoutine's opening windows for recently queried days,
        // validated against an exact copy of the hours on every use.
        internal OpeningCache? OpeningCache;
    }

    public sealed class OpeningWindow
    {
        // Monday=0 through Sunday=6. An interval ending before it starts crosses midnight.
        public int Day { get; set; }
        public int FromMinute { get; set; }
        public int ToMinute { get; set; }
    }

    public sealed class TravelPreferences
    {
        public bool OwnsCar { get; set; }
        public bool AllowTransit { get; set; } = true;
        public double WalkingToleranceMetres { get; set; } = 1200;
        public double FareWeightMinutes { get; set; } = 2;
    }

    public sealed class RouteLeg
    {
        private string serviceId = "";
        public Position From { get; set; }
        public Position To { get; set; }
        public TravelMode Mode { get; set; }
        public double Minutes { get; set; }
        /// <summary>Native provenance on the wire, e.g. native:road:v1:&lt;topology&gt;:&lt;from&gt;:&lt;to&gt;. Setting it
        /// (when planned, copied or loaded) also decodes a native road tag into transient fields.</summary>
        public string ServiceId
        {
            get => serviceId;
            set { serviceId = value; RoadTag = RoadTag.Decode(value); }
        }
        /// <summary>Transient decoding of ServiceId; never serialized.</summary>
        internal RoadTag RoadTag { get; private set; }
    }

    internal enum RoadTagKind : byte { None, V1, Fragment1 }

    /// <summary>
    /// A native road tag split once: the kind ("v1" or "fragment1"), the position of the topology segment
    /// within the tag and the two native node indices. Kind None means the tag is not a well-formed
    /// native:road tag: exactly six colon-separated parts, "native", "road", "v1" or "fragment1", any
    /// topology text, then two node indices accepted by int.TryParse with NumberStyles.None. Callers still
    /// check the topology against their own network on every use.
    /// </summary>
    internal readonly struct RoadTag
    {
        private RoadTag(RoadTagKind kind, int topologyStart, int topologyLength, int from, int to)
        { Kind = kind; TopologyStart = topologyStart; TopologyLength = topologyLength; From = from; To = to; }
        public RoadTagKind Kind { get; }
        public int TopologyStart { get; }
        public int TopologyLength { get; }
        public int From { get; }
        public int To { get; }

        private const string Prefix = "native:road:";

        internal static RoadTag Decode(string? service)
        {
            if (service == null || !service.StartsWith(Prefix, StringComparison.Ordinal)) return default;
            int kindEnd = service.IndexOf(':', Prefix.Length);
            if (kindEnd < 0) return default;
            int topologyEnd = service.IndexOf(':', kindEnd + 1);
            if (topologyEnd < 0) return default;
            int fromEnd = service.IndexOf(':', topologyEnd + 1);
            if (fromEnd < 0 || service.IndexOf(':', fromEnd + 1) >= 0) return default;
            RoadTagKind kind;
            int kindLength = kindEnd - Prefix.Length;
            if (kindLength == 2 && string.CompareOrdinal(service, Prefix.Length, "v1", 0, 2) == 0) kind = RoadTagKind.V1;
            else if (kindLength == 9 && string.CompareOrdinal(service, Prefix.Length, "fragment1", 0, 9) == 0) kind = RoadTagKind.Fragment1;
            else return default;
            if (!int.TryParse(service.AsSpan(topologyEnd + 1, fromEnd - topologyEnd - 1), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int from)
                || !int.TryParse(service.AsSpan(fromEnd + 1), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int to)) return default;
            return new RoadTag(kind, kindEnd + 1, topologyEnd - kindEnd - 1, from, to);
        }

        /// <summary>True when the decoded tag names this topology, compared in place without allocation.</summary>
        internal bool IsTopology(string service, string? topology) => Kind != RoadTagKind.None && topology != null
            && topology.Length == TopologyLength && string.CompareOrdinal(service, TopologyStart, topology, 0, TopologyLength) == 0;
    }

    public sealed class Route
    {
        private double? estimatedMinutes;
        public List<RouteLeg> Legs { get; set; } = new List<RouteLeg>();
        public double Minutes
        {
            get { double total = 0; foreach (var leg in Legs) total += leg.Minutes; return total; }
        }
        /// <summary>Transient planning ETA, never movement progress or persisted journey timing.</summary>
        public double EstimatedMinutes => estimatedMinutes ?? Minutes;
        /// <summary>Attach one snapshot estimate to this caller-owned route without changing its legs.</summary>
        public Route WithEstimate(double total)
        {
            double nominal = Minutes;
            if (!Position.Finite(total) || !Position.Finite(nominal) || nominal <= 0 || total < nominal)
                throw new ArgumentOutOfRangeException(nameof(total), "Estimated travel time must be finite and at least the nominal route duration.");
            estimatedMinutes = total;
            return this;
        }
        public Position PositionAt(double elapsed)
        {
            if (Legs.Count == 0) throw new InvalidOperationException("A route must contain a leg.");
            foreach (var leg in Legs)
            {
                if (elapsed <= leg.Minutes) return Position.Lerp(leg.From, leg.To, Math.Max(0, elapsed / leg.Minutes));
                elapsed -= leg.Minutes;
            }
            return Legs[Legs.Count - 1].To;
        }
    }

    public interface IRoutePlanner
    {
        Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences);
    }

    /// <summary>Optional conservative bound using the planner's current walking pace.
    /// It can reject an impossible time window, never establish an available route.</summary>
    public interface IWalkingTimeBound
    {
        double MinimumWalkingMinutes(Place from, Place to);
    }

    /// <summary>Current planner pace, used only to bound food searches by a finite meal window.</summary>
    public interface IFoodWalkingPace
    {
        double WalkingMetresPerMinute { get; }
    }

    public interface ICitizenRoutePlanner : IRoutePlanner
    {
        Route? Plan(Place from, Place to, double departureMinute, CitizenState citizen);
    }

    public sealed class CarState
    {
        public string ModelId { get; set; } = "";
        public int PaintSeed { get; set; }
        public string Id { get; set; } = "";
        // A logical curb position, not a native vehicle or parking reservation.
        public Position ParkedPosition { get; set; }
    }

    public sealed class CommutePreparation
    {
        public string PlaceId { get; set; } = "";
        public double WorkStartsAt { get; set; }
        public double WorkEndsAt { get; set; }
        public double PreparedAt { get; set; }
        // Zero records a failed estimate; positive values are durations, never cached routes.
        public double TravelMinutes { get; set; }
    }

    public sealed class CitizenState
    {
        public WeatherCommitment? W { get; set; }
        public CitizenIdentity Identity { get; set; } = new CitizenIdentity();
        public string HomeId { get; set; } = "";
        public string DayPlaceId { get; set; } = "";
        public string ShopId { get; set; } = "";
        public string LeisureId { get; set; } = "";
        public TravelPreferences Preferences { get; set; } = new TravelPreferences();
        public CommutePreparation CommutePreparation { get; set; } = new CommutePreparation();
        public MealOuting? MealOuting { get; set; }
        public OptionalStopState? OptionalStop { get; set; }
        // Null means no recorded chance (including migrated visits); never infer it on reload.
        public bool? OptionalStopChoice { get; set; }
        public CarState? Car { get; set; }
        public double JoinedMinute { get; set; }
        public int LastEmploymentCheckDay { get; set; }
        public int JobChanges { get; set; }
        public CitizenPhase Phase { get; set; }
        public Activity Activity { get; set; }
        public string PlaceId { get; set; } = "";
        public string DestinationId { get; set; } = "";
        public Activity DestinationActivity { get; set; }
        public Route? Journey { get; set; }
        public double DepartureMinute { get; set; }
        public double NextEventMinute { get; set; }
        public double ActivityUntilMinute { get; set; }
        public int CompletedJourneys { get; set; }
        public int MissedActivities { get; set; }
        public List<VisitRecord> RecentVisits { get; set; } = new List<VisitRecord>();
        /// <summary>Lifestyle traits, generated once from the city seed and this stable ID (schema 17).
        /// Required for every resident, even without a food scope; immutable once generated.</summary>
        public ResidentTraits? Traits { get; set; }
        // Transient and never serialized: per-resident routine hashes derived from Identity.Id and the
        // city seed. RoutineKeys.For rebuilds it when either differs; event copies share it.
        internal RoutineKeys? RoutineKeys;
    }

    public sealed class VisitRecord
    {
        public double AtMinute { get; set; }
        public string PlaceId { get; set; } = "";
        public Activity Activity { get; set; }
    }

    public sealed class CityState
    {
        public const int CurrentSchema = 24;
        public string MenuPolicy { get; set; } = FoodMenuPolicy.Wire;
        public int WeatherStartsDay { get; set; }
        public PopulationCapabilities Capabilities { get; set; } = new PopulationCapabilities();
        public int Schema { get; set; } = CurrentSchema;
        public int TravelTimingVersion { get; set; } = 1;
        public double TravelClockRate { get; set; } = 1;
        public string TrafficTopologyId { get; set; } = "";
        public TrafficKernelState? Traffic { get; set; }
        public FoodCommerceState? Food { get; set; }
        // The live event engine owns mutable queues; serialization asks it for one coherent snapshot.
        internal Action? SnapshotSynchronizer;
        public void PrepareForSave()
        {
            SnapshotSynchronizer?.Invoke();
            Food?.Outflow?.Advance((int)(Minute / 1440));
        }
        public string RloVersion { get; set; } = "";
        public string PopulationKey { get; set; } = "";
        public int Seed { get; set; }
        public double Minute { get; set; }
        public List<Place> Places { get; set; } = new List<Place>();
        public List<CitizenState> Citizens { get; set; } = new List<CitizenState>();
        // The RLO import remains immutable even as this mod's own roster changes.
        public List<CitizenIdentity> SourceIdentities { get; set; } = new List<CitizenIdentity>();
        public int NextResidentSequence { get; set; } = 1;
        public int PopulationTarget { get; set; }
        public int InitialImportedResidents { get; set; }
    }

}
