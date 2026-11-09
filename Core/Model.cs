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

    public sealed class Place : IPlaceView
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
        // F9e: the read-only view (getters only; no field, wire or save change).
        string IPlaceView.Id => Id;
        string IPlaceView.Name => Name;
        string IPlaceView.District => District;
        PlaceKind IPlaceView.Kind => Kind;
        Position IPlaceView.Position => Position;
        int IPlaceView.Capacity => Capacity;
        int IPlaceView.OpenMinute => OpenMinute;
        int IPlaceView.CloseMinute => CloseMinute;
        double IPlaceView.AttractionWeight => AttractionWeight;
        int IPlaceView.NativeTrafficIndex => NativeTrafficIndex;
        string IPlaceView.EvidenceSource => EvidenceSource;
        double IPlaceView.ResidentialAreaSquareMetres => ResidentialAreaSquareMetres;
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
        private Position from, to;
        private TravelMode mode;
        private double minutes;
        private string serviceId = "";
        public Position From { get => from; set { Mutable(); from = value; } }
        public Position To { get => to; set { Mutable(); to = value; } }
        public TravelMode Mode { get => mode; set { Mutable(); mode = value; } }
        public double Minutes { get => minutes; set { Mutable(); minutes = value; } }
        /// <summary>Native provenance on the wire, e.g. native:road:v1:&lt;topology&gt;:&lt;from&gt;:&lt;to&gt;. Setting it
        /// (when planned, copied or loaded) also decodes a native road tag into transient fields.</summary>
        public string ServiceId
        {
            get => serviceId;
            set { Mutable(); serviceId = value; RoadTag = RoadTag.Decode(value); }
        }
        /// <summary>Transient decoding of ServiceId; never serialized.</summary>
        internal RoadTag RoadTag { get; private set; }
        /// <summary>True once the route holding this leg was committed as a journey (F7c); every setter then refuses.</summary>
        internal bool Frozen { get; private set; }
        internal void Freeze() => Frozen = true;
        private void Mutable() { if (Frozen) throw new InvalidOperationException("A committed route leg is immutable."); }
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

    /// <summary>
    /// A route's own leg list (F7c). It has the List API the planners and tests use, and it never aliases a caller's
    /// list: assigning a <see cref="List{T}"/> copies its legs. Once its route is committed it refuses every edit, through
    /// any reference to it, so a committed route can be shared by save snapshots. JSON reads and writes it as an array.
    /// </summary>
    public sealed class RouteLegs : IList<RouteLeg>, IReadOnlyList<RouteLeg>
    {
        private readonly List<RouteLeg> items;
        public RouteLegs() { items = new List<RouteLeg>(); }
        public RouteLegs(IEnumerable<RouteLeg> legs) { items = new List<RouteLeg>(legs); }
        public static implicit operator RouteLegs(List<RouteLeg> legs) => new RouteLegs(legs ?? throw new ArgumentNullException(nameof(legs)));

        /// <summary>True once the owning route was committed.</summary>
        public bool IsReadOnly { get; private set; }
        internal void Freeze() => IsReadOnly = true;
        private void Mutable() { if (IsReadOnly) throw new InvalidOperationException("A committed route is immutable."); }

        public int Count => items.Count;
        public RouteLeg this[int index] { get => items[index]; set { Mutable(); items[index] = value; } }
        public void Add(RouteLeg leg) { Mutable(); items.Add(leg); }
        public void AddRange(IEnumerable<RouteLeg> legs) { Mutable(); items.AddRange(legs); }
        public void Insert(int index, RouteLeg leg) { Mutable(); items.Insert(index, leg); }
        public void RemoveAt(int index) { Mutable(); items.RemoveAt(index); }
        public bool Remove(RouteLeg leg) { Mutable(); return items.Remove(leg); }
        public void Clear() { Mutable(); items.Clear(); }
        public bool Contains(RouteLeg leg) => items.Contains(leg);
        public int IndexOf(RouteLeg leg) => items.IndexOf(leg);
        public void CopyTo(RouteLeg[] array, int index) => items.CopyTo(array, index);
        /// <summary>The list's own struct enumerator, so a foreach over the legs allocates nothing.</summary>
        public List<RouteLeg>.Enumerator GetEnumerator() => items.GetEnumerator();
        IEnumerator<RouteLeg> IEnumerable<RouteLeg>.GetEnumerator() => items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => items.GetEnumerator();
    }

    public sealed class Route
    {
        private double? estimatedMinutes;
        private RouteLegs legs = new RouteLegs();
        private double committedMinutes;
        public RouteLegs Legs
        {
            get => legs;
            set { if (legs.IsReadOnly) throw new InvalidOperationException("A committed route is immutable."); legs = value ?? throw new ArgumentNullException(nameof(value)); }
        }
        /// <summary>The sum of the leg durations in leg order; computed once at commit for a committed route.</summary>
        public double Minutes => legs.IsReadOnly ? committedMinutes : Sum();
        private double Sum() { double total = 0; foreach (var leg in legs) total += leg.Minutes; return total; }
        /// <summary>True once <see cref="Commit"/> froze this route (F7c).</summary>
        public bool IsCommitted => legs.IsReadOnly;
        /// <summary>
        /// Freeze a route as it becomes a journey (F7c, plan section 3.4): the leg list and every leg refuse edits from now
        /// on, and the total minutes are computed once, with the same summation order as before. Save snapshots then share
        /// the route instead of copying its legs. Idempotent. A null leg refuses the commit.
        /// </summary>
        internal Route Commit()
        {
            if (legs.IsReadOnly) return this;
            foreach (var leg in legs) if (leg == null) throw new InvalidOperationException("A committed route has a null leg.");
            foreach (var leg in legs) leg.Freeze();
            committedMinutes = Sum();
            legs.Freeze();
            return this;
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

    public class CitizenState : IPersonView
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
        /// <summary>Schema 26: day the current DayPlaceId assignment began (the person's Employment start); zero while
        /// unassigned. Bookkeeping only: no routine, choice or money reads it.</summary>
        public int EmploymentStartDay { get; set; }
        public EmploymentAccrual Accrual { get; set; }
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
        /// <summary>F2e (schema 35): game minutes of player hold already charged to the current journey, at most
        /// <see cref="PlayerHolds.MaximumMinutesPerJourney"/>. Zero unless travelling; cleared when the journey ends or is
        /// replaced, so reloading during a blockage cannot reset the allowance.</summary>
        public double PlayerHoldMinutes { get; set; }
        /// <summary>Lifestyle traits, generated once from the city seed and this stable ID (schema 17).
        /// Required for every resident, even without a food scope; immutable once generated.</summary>
        public ResidentTraits? Traits { get; set; }
        // Transient and never serialized: per-resident routine hashes derived from Identity.Id and the
        // city seed. RoutineKeys.For rebuilds it when either differs; event copies share it.
        internal RoutineKeys? RoutineKeys;
        // Transient and never serialized: the visitor person whose routine this is (schema 27), shared by event
        // copies. Null for residents. Visitors keep no RecentVisits history beyond their latest arrival.
        internal VisitorPerson? Visitor;
        // Transient copy of the visitor's carried income source, edited by an event's working copy and published at
        // commit (VisitorSchedules.Sync), so an abandoned event never changes it. Always null for residents.
        internal ResidentIncomeSource? VisitorCarried;
        /// <summary>True for a schema-27 visitor's routine (also on an event's working copy).</summary>
        public bool IsVisitor => Visitor != null;
        // F9e: the read-only view (getters only; no field, wire or save change).
        string IPersonView.Id => Identity.Id;
        string IPersonView.Name => Identity.Name;
        string IPersonView.District => Identity.District;
        int IPersonView.Age => Identity.Age;
        bool IPersonView.Female => Identity.Female;
        Persona IPersonView.Persona => Identity.Persona;
        string IPersonView.HomeId => HomeId;
        string IPersonView.DayPlaceId => DayPlaceId;
        string IPersonView.ShopId => ShopId;
        string IPersonView.LeisureId => LeisureId;
        CitizenPhase IPersonView.Phase => Phase;
        Activity IPersonView.Activity => Activity;
        string IPersonView.PlaceId => PlaceId;
        string IPersonView.DestinationId => DestinationId;
        Activity IPersonView.DestinationActivity => DestinationActivity;
        double IPersonView.DepartureMinute => DepartureMinute;
        double IPersonView.NextEventMinute => NextEventMinute;
        double IPersonView.ActivityUntilMinute => ActivityUntilMinute;
        int IPersonView.CompletedJourneys => CompletedJourneys;
        int IPersonView.MissedActivities => MissedActivities;
        double IPersonView.JoinedMinute => JoinedMinute;
        int IPersonView.EmploymentStartDay => EmploymentStartDay;
        int IPersonView.JobChanges => JobChanges;
        double IPersonView.PlayerHoldMinutes => PlayerHoldMinutes;
        bool IPersonView.IsVisitor => IsVisitor;
    }

    public sealed class VisitRecord
    {
        public double AtMinute { get; set; }
        public string PlaceId { get; set; } = "";
        public Activity Activity { get; set; }
    }

    public sealed class CityState
    {
        /// <summary>C5c: the next writer after F5e's workplace-capacity schema; it adds <see cref="Research"/>.</summary>
        public const int ResearchSchema = PersonTable.WorkplaceCapacitySchema + 1;
        /// <summary>E6c: the next writer after C5c's research schema; it adds <see cref="Vendors"/> and the scope channel.</summary>
        public const int VendorSchema = ResearchSchema + 1;
        public const int EconomicLedgerSchema = VendorSchema + 1;
        public const int AttendanceSchema = EconomicLedgerSchema + 1;
        public const int ChangeJournalSchema = AttendanceSchema + 1;
        /// <summary>F7d: the next writer after F6g's change-journal schema. Same entity shape; the save string is the compact
        /// columnar wire (candidate C), not JSON of this graph.</summary>
        public const int CompactSchema = ChangeJournalSchema + 1;
        /// <summary>F2e: the next writer after F7d's compact schema, in the same compact wire. It adds the per-journey
        /// <see cref="CitizenState.PlayerHoldMinutes"/> and the city-wide <see cref="PlayerHoldSequence"/>.</summary>
        public const int PlayerHoldSchema = CompactSchema + 1;
        /// <summary>E5b4: the next writer after F2e's player-hold schema, in the same compact wire and entity shape. New cities
        /// are created with <see cref="FoodNameIdentity.DynamicWire"/>, which this writer executes (Decision 9).</summary>
        public const int HeldPolicySchema = PlayerHoldSchema + 1;
        public const int CurrentSchema = HeldPolicySchema;
        /// <summary>The one executable menu policy of a schema: DynamicWire from <see cref="HeldPolicySchema"/>, the
        /// original-name policy for 25 to 35, and E4's code policy for 23 and 24.</summary>
        public static string ExecutableMenuPolicy(int schema)
            => schema >= HeldPolicySchema ? FoodNameIdentity.DynamicWire : schema >= 25 ? FoodNameIdentity.Wire : FoodMenuPolicy.Wire;
        private string? menuPolicy;
        /// <summary>Until set, the executable policy of this state's <see cref="Schema"/>: a new city (current writer) holds
        /// <see cref="FoodNameIdentity.DynamicWire"/>; a state relabelled to a frozen writer keeps that writer's policy.</summary>
        public string MenuPolicy { get => menuPolicy ?? ExecutableMenuPolicy(Schema); set => menuPolicy = value; }
        public List<string> ProductNames { get; set; } = new List<string>();
        // Reserved next-day policy boundary; E5d adds no dynamic choice/market execution.
        public int NamePolicyStartsDay { get; set; }
        public int WeatherStartsDay { get; set; }
        public PopulationCapabilities Capabilities { get; set; } = new PopulationCapabilities();
        public int Schema { get; set; } = CurrentSchema;
        public int TravelTimingVersion { get; set; } = 1;
        public double TravelClockRate { get; set; } = 1;
        public string TrafficTopologyId { get; set; } = "";
        public TrafficKernelState? Traffic { get; set; }
        public FoodCommerceState? Food { get; set; }
        public EconomicLedger Economy { get; set; } = new EconomicLedger();
        // The live event engine owns mutable queues; serialization asks it for one coherent snapshot.
        internal Action? SnapshotSynchronizer;
        public void PrepareForSave()
        {
            SnapshotSynchronizer?.Invoke();
            foreach (var visitor in Visitors) if (visitor.Routine != null) VisitorSchedules.Sync(visitor);
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
        /// <summary>Schema 26 visitor persons, ordinal by ID. In writer 26 each is the represented consumer of one
        /// resident's slot (factor 2/3), eating on today's excursion schedule; see <see cref="PersonTable"/>.</summary>
        public List<VisitorPerson> Visitors { get; set; } = new List<VisitorPerson>();
        /// <summary>Next <c>pv:&lt;n&gt;</c> sequence. Writer 26 allocates none, so it equals one plus the pv: visitors.</summary>
        public int NextVisitorSequence { get; set; } = 1;
        /// <summary>Version of the derived (never stored) visitor identity policy (contract B6).</summary>
        public int IdentityPolicyVersion { get; set; } = IdentityPolicy.CurrentVersion;
        /// <summary>Saved ordinal station table that visitor stays reference by index; empty until visitors arrive by portal.</summary>
        public List<string> Portals { get; set; } = new List<string>();
        /// <summary>Schema 27: one routine per visitor, in the order of <see cref="Visitors"/>. Each is scheduled by the
        /// same event queue as the residents; its identity is derived, its home is its portal place (off-map time).</summary>
        public List<VisitorRoutine> VisitorRoutines { get; set; } = new List<VisitorRoutine>();
        /// <summary>C5c: this city's research orders and kept reports. A fresh city starts with an empty ledger.</summary>
        public ResearchLedger Research { get; set; } = new ResearchLedger();
        /// <summary>E6c: the Street Food vendor rows, ordinal by vehicle id. Their places are rebuilt from this table
        /// on load and never stored in <see cref="Places"/>. A fresh city starts with an empty table.</summary>
        public VendorTable Vendors { get; set; } = new VendorTable();
        /// <summary>F2e (schema 35): sequence number of the last applied player hold; the next hold takes this plus one.
        /// A replay segment starts and ends at the sequences saved in its two checkpoints.</summary>
        public long PlayerHoldSequence { get; set; }
    }

}
