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
        public string BusinessType { get; set; } = "";
        internal OpeningCache? OpeningCache;
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
        public string ServiceId
        {
            get => serviceId;
            set { Mutable(); serviceId = value; RoadTag = RoadTag.Decode(value); }
        }
        internal RoadTag RoadTag { get; private set; }
        internal bool Frozen { get; private set; }
        internal void Freeze() => Frozen = true;
        private void Mutable() { if (Frozen) throw new InvalidOperationException("A committed route leg is immutable."); }
    }

    internal enum RoadTagKind : byte { None, V1, Fragment1 }

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

        internal bool IsTopology(string service, string? topology) => Kind != RoadTagKind.None && topology != null
            && topology.Length == TopologyLength && string.CompareOrdinal(service, TopologyStart, topology, 0, TopologyLength) == 0;
    }

    public sealed class RouteLegs : IList<RouteLeg>, IReadOnlyList<RouteLeg>
    {
        private readonly List<RouteLeg> items;
        public RouteLegs() { items = new List<RouteLeg>(); }
        public RouteLegs(IEnumerable<RouteLeg> legs) { items = new List<RouteLeg>(legs); }
        public static implicit operator RouteLegs(List<RouteLeg> legs) => new RouteLegs(legs ?? throw new ArgumentNullException(nameof(legs)));

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
        public double Minutes => legs.IsReadOnly ? committedMinutes : Sum();
        private double Sum() { double total = 0; foreach (var leg in legs) total += leg.Minutes; return total; }
        public bool IsCommitted => legs.IsReadOnly;
        internal Route Commit()
        {
            if (legs.IsReadOnly) return this;
            foreach (var leg in legs) if (leg == null) throw new InvalidOperationException("A committed route has a null leg.");
            foreach (var leg in legs) leg.Freeze();
            committedMinutes = Sum();
            legs.Freeze();
            return this;
        }
        public double EstimatedMinutes => estimatedMinutes ?? Minutes;
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

    public interface IWalkingTimeBound
    {
        double MinimumWalkingMinutes(Place from, Place to);
    }

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
        public Position ParkedPosition { get; set; }
    }

    public sealed class CommutePreparation
    {
        public string PlaceId { get; set; } = "";
        public double WorkStartsAt { get; set; }
        public double WorkEndsAt { get; set; }
        public double PreparedAt { get; set; }
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
        public bool? OptionalStopChoice { get; set; }
        public CarState? Car { get; set; }
        public double JoinedMinute { get; set; }
        public int LastEmploymentCheckDay { get; set; }
        public int JobChanges { get; set; }
        public int EmploymentStartDay { get; set; }
        public EmploymentAccrual Accrual { get; set; }
        public ResidentBudget? Wallet { get; set; }
        public DayPlan? Plan { get; set; }
        public PersonNeeds? Needs { get; set; }
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
        public double PlayerHoldMinutes { get; set; }
        public ResidentTraits? Traits { get; set; }
        internal RoutineKeys? RoutineKeys;
        internal VisitorPerson? Visitor;
        internal ResidentIncomeSource? VisitorCarried;
        public bool IsVisitor => Visitor != null;
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
        public const int CurrentSchema = 1;
        public string MenuPolicy { get; set; } = FoodNameIdentity.DynamicWire;
        public List<string> ProductNames { get; set; } = new List<string>();
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
        public List<CitizenIdentity> SourceIdentities { get; set; } = new List<CitizenIdentity>();
        public int NextResidentSequence { get; set; } = 1;
        public int PopulationTarget { get; set; }
        public int InitialImportedResidents { get; set; }
        public List<VisitorPerson> Visitors { get; set; } = new List<VisitorPerson>();
        public int NextVisitorSequence { get; set; } = 1;
        public int IdentityPolicyVersion { get; set; } = IdentityPolicy.CurrentVersion;
        public List<string> Portals { get; set; } = new List<string>();
        public List<VisitorRoutine> VisitorRoutines { get; set; } = new List<VisitorRoutine>();
        public ResearchLedger Research { get; set; } = new ResearchLedger();
        public VendorTable Vendors { get; set; } = new VendorTable();
        public long PlayerHoldSequence { get; set; }
    }

}
