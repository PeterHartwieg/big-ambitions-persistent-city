using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.Tests;

/// <summary>
/// Issue #128 replay: a 957-resident / 1,914-consumer city on a 24x24 road grid with persistent car
/// traffic, household finances, three food scopes and optional stops, driven frame by frame through
/// the same loop shape as PopulationRuntime.AdvancePopulation at 30 FPS (PopulationFrameBudget caps).
/// Each AdvanceFrameSlice call (one unit) and each frame's advancement interval is timed. The detached
/// planner, market and stop environment are native doubles: they count the calls a unit makes into
/// each seam but cannot reproduce native cost. Also compiled by tools/rush-spike-replay.
/// </summary>
public static class RushSpikeReplay
{
    public const string Native = "1280000000000000000000000000000000000000000000000000000000000128";
    private const int Side = 24;
    private const double Spacing = 50, ClockRate = 1.0 / 6, WalkMetresPerMinute = 20;

    public sealed class Result
    {
        public string SaveSha256 = "";
        public long Frames, Units, CitizenEvents, TrafficEvents, Purchases, CompletedJourneys, Stops, StopSeamCalls, RouteCalls;
        public double FrameP50, FrameP95, FrameP99, FrameMax, UnitMax, SyncMax;
        public int FrameSpikes30, UnitSpikes30, CollectionsInMaxFrame, MaxUnitsInFrame, MaxStopSeamCallsInUnit, MaxRouteCallsInUnit;
        public double MaxFrameMinute, MaxUnitMinute;
        public string MaxUnitKind = "";
        public int PeakVehicles, PeakTravelling;
    }

    private static int Node(int x, int y) => y * Side + x;
    private static Position At(int node) => new Position(node % Side * Spacing, 0, node / Side * Spacing);
    private static int NodeOf(Position p) => Node((int)Math.Round(p.X / Spacing), (int)Math.Round(p.Z / Spacing));

    public sealed class Planner : IRoutePlanner, ICitizenRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; }
        public long Calls;
        public Planner()
        {
            var nodes = new List<TrafficNetworkNode>();
            for (int y = 0; y < Side; y++)
                for (int x = 0; x < Side; x++)
                {
                    var next = new List<int>();
                    if (x > 0) next.Add(Node(x - 1, y)); if (x + 1 < Side) next.Add(Node(x + 1, y));
                    if (y > 0) next.Add(Node(x, y - 1)); if (y + 1 < Side) next.Add(Node(x, y + 1));
                    nodes.Add(new TrafficNetworkNode { Id = Node(x, y), Position = At(Node(x, y)), KilometresPerHour = 36,
                        AllowsCar = true, Boundary = true, Successors = next.ToArray() });
                }
            TrafficNetwork = new TrafficNetwork(Native, nodes, ClockRate);
        }
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => Walk(from, to);
        public Route? Plan(Place from, Place to, double minute, CitizenState citizen)
        {
            Calls++;
            int a = NodeOf(from.Position), b = NodeOf(to.Position);
            if (a == b || citizen.Car == null || !citizen.Preferences.OwnsCar
                || Position.Distance(citizen.Car.ParkedPosition, from.Position) > 0.01) return Walk(from, to);
            var route = new Route();
            int x = a % Side, y = a / Side, tx = b % Side, ty = b / Side, at = a;
            while (at != b)
            {
                if (x != tx) x += Math.Sign(tx - x); else y += Math.Sign(ty - y);
                int next = Node(x, y);
                TrafficNetwork!.TryGetEdge(at, next, out var edge);
                route.Legs.Add(new RouteLeg { From = At(at), To = At(next), Mode = TravelMode.Car, Minutes = edge.FreeFlowMinutes,
                    ServiceId = "native:road:v1:" + Native + ":" + at.ToString(CultureInfo.InvariantCulture) + ":" + next.ToString(CultureInfo.InvariantCulture) });
                at = next;
            }
            return route;
        }
        private Route Walk(Place from, Place to) => new Route { Legs = { new RouteLeg { From = from.Position, To = to.Position,
            Mode = TravelMode.Walk, Minutes = Math.Max(1, Position.Distance(from.Position, to.Position) / WalkMetresPerMinute) } } };
    }

    public sealed class Market : IFoodMarket, IFoodSaleEvidence
    {
        private static readonly (string Place, string Business, string[] Products)[] Shops =
        {
            ("shop-000", "b1", new[] { FoodProducts.Croissant, FoodProducts.Burger }),
            ("shop-001", "b2", new[] { FoodProducts.HotDog }),
            ("shop-002", "b3", new[] { FoodProducts.Burger, FoodProducts.HotDog, FoodProducts.Croissant }),
        };
        private readonly HashSet<string> paid = new HashSet<string>(StringComparer.Ordinal);
        public long Reads;
        public double EarliestMinute => 0;
        private readonly bool nativePrices;
        public Market(bool nativePrices = false) { this.nativePrices = nativePrices; }
        // Keep the original cheap menu as well as native price levels (hotdog is a salad-price proxy).
        private Money Price(string product) => product == FoodProducts.Croissant ? Money.FromCents(nativePrices ? 924 : 350)
            : product == FoodProducts.Burger ? Money.FromCents(nativePrices ? 1250 : 725) : Money.FromCents(nativePrices ? 1669 : 480);
        public IReadOnlyList<FoodBusiness> Read(double minute)
        {
            Reads++;
            return Shops.Select(s => new FoodBusiness { Id = s.Business, PlaceId = s.Place, Name = s.Place, MealsPerHour = 600,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 360, ToMinute = 1380 }).ToList(),
                Offers = s.Products.Select(p => new FoodOffer { ProductId = p, Price = Price(p), Units = 100000 }).ToList() }).ToList();
        }
        public FoodSale Buy(ResidentMeal meal, double minute) { paid.Add(meal.IntentId); return new FoodSale(true, "detached confirmed"); }
        public bool IsConfirmedPaid(ResidentMeal meal) => paid.Contains(meal.IntentId);
    }

    /// <summary>Stands in for Runtime's OptionalStopEnvironment: fixed spots, every call counted.</summary>
    public sealed class Stops : IOptionalStopEnvironment
    {
        private readonly Dictionary<string, OptionalStopSpot[]> cache = new Dictionary<string, OptionalStopSpot[]>(StringComparer.Ordinal);
        public long Calls;
        public bool Raining => false;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
        {
            Calls++;
            if (kind == DwellKind.Audience) return Array.Empty<OptionalStopSpot>();
            if (kind == DwellKind.Resting && !place.Id.StartsWith("scene-park:", StringComparison.Ordinal)) return Array.Empty<OptionalStopSpot>();
            if (kind == DwellKind.ChatOutsideVenue && !place.Id.EndsWith("0", StringComparison.Ordinal)) return Array.Empty<OptionalStopSpot>();
            string key = place.Id + ":" + kind;
            if (cache.TryGetValue(key, out var known)) return known;
            var spots = Enumerable.Range(0, 6).Select(i => new OptionalStopSpot { Id = key + ":" + i, TargetId = place.Id,
                Position = new Position(place.Position.X + 2 + i, 0, place.Position.Z + 2), Facing = place.Position }).ToArray();
            cache[key] = spots; return spots;
        }
        public OptionalStopAvailability Available(OptionalStopState stop) { Calls++; return OptionalStopAvailability.Ready; }
    }

    public static CityState Create(int residents = 957, int seed = 128)
    {
        var random = new Random(seed);
        var places = new List<Place>();
        void Add(string prefix, int count, PlaceKind kind, int capacity, int open = 0, int close = 1440)
        {
            for (int i = 0; i < count; i++)
                places.Add(new Place { Id = prefix + i.ToString("D3", CultureInfo.InvariantCulture), Name = prefix + i,
                    District = random.Next(2) == 0 ? "a" : "b", Kind = kind, Capacity = capacity,
                    Position = At(random.Next(Side * Side)), OpenMinute = open, CloseMinute = close });
        }
        // 1,058 places, as in the 6 Nov rig city: 331 households, 383 workplaces, shops, leisure and parks.
        Add("home-", 331, PlaceKind.Home, 4);
        Add("work-", 383, PlaceKind.Work, 4, 420, 1140);
        Add("shop-", 220, PlaceKind.Shop, 20, 540, 1260);
        Add("leisure-", 80, PlaceKind.Leisure, 40, 600, 1380);
        Add("scene-park:", 20, PlaceKind.Leisure, 60);
        Add("school-", 24, PlaceKind.Study, 60, 480, 900);
        var identities = Enumerable.Range(0, residents).Select(i => new CitizenIdentity
        {
            Id = "rlo:rush:c" + i.ToString("D4", CultureInfo.InvariantCulture), Name = "Citizen " + i, Age = 18 + i % 60,
            District = i % 2 == 0 ? "a" : "b", Persona = (Persona)(i % 8), AppearanceSeed = i + 1
        });
        var city = Population.Create(identities, places, "rush-spike-128", "0.41.1", seed, 1440 + 300);
        var byId = city.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        foreach (var person in city.Citizens)
            if (person.Preferences.OwnsCar)
                person.Car = new CarState { Id = person.Identity.Id + ":car", ParkedPosition = byId[person.HomeId].Position };
        city.Food = new FoodCommerceState
        {
            RepresentationFactor = 2, MealRhythmVersion = MealRhythm.PolicyVersion,
            Shops = new List<FoodScope>
            {
                new FoodScope { PlaceId = "shop-000", BusinessId = "b1", ProductId = FoodProducts.Croissant, AdditionalProductIds = { FoodProducts.Burger } },
                new FoodScope { PlaceId = "shop-001", BusinessId = "b2", ProductId = FoodProducts.HotDog },
                new FoodScope { PlaceId = "shop-002", BusinessId = "b3", ProductId = FoodProducts.Burger, AdditionalProductIds = { FoodProducts.HotDog, FoodProducts.Croissant } },
            }
        };
        return city;
    }

    public static CitySimulator Attach(CityState state, Planner planner, Market market, Stops stops)
    {
        var city = new CitySimulator(state, planner);
        city.AttachFoodMarket(market); city.EnableHouseholdFinances();
        city.AttachOptionalStops(stops, OptionalStopPolicy.ApprovedChoicePercentage);
        return city;
    }

    /// <summary>Warm up uninterrupted to <paramref name="from"/>, then advance frame by frame to
    /// <paramref name="to"/> at the rig's pace (about 98.6 frames per game minute at 30 FPS).</summary>
    public static Result Run(double from, double to, double framesPerGameMinute = 98.6, int fps = 30, bool nativePrices = false)
    {
        var planner = new Planner(); var market = new Market(nativePrices); var stops = new Stops();
        var state = Create();
        var city = Attach(state, planner, market, stops);
        while (!city.AdvanceTo(from, 256).CaughtUp) { }
        // Collect the warm-up's and earlier tests' garbage before the timed window, so a collection of memory this replay
        // did not allocate in the window cannot land in one of its intervals. Collections caused by the window's own
        // allocations still count.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var result = new Result();
        var frames = new List<double>();
        double ms = 1000.0 / Stopwatch.Frequency, clock = from;
        while (clock < to || !city.IsCaughtUpTo(to))
        {
            clock = Math.Min(to, clock + 1 / framesPerGameMinute);
            double target = Math.Max(state.Minute, clock);
            var budget = new PopulationFrameBudget(1.0 / fps, false);
            int collections = GC.CollectionCount(0);
            long started = Stopwatch.GetTimestamp();
            int citizens = 0, units = 0;
            city.RefreshOptionalStops();
            for (int step = 0; step < budget.Units; step++)
            {
                long stopCalls = stops.Calls, routeCalls = planner.Calls, unitStarted = Stopwatch.GetTimestamp();
                var advanced = city.AdvanceFrameSlice(target, 1);
                double unit = (Stopwatch.GetTimestamp() - unitStarted) * ms;
                units += advanced.Events; citizens += advanced.CitizenEvents;
                result.Units += advanced.Events; result.CitizenEvents += advanced.CitizenEvents; result.TrafficEvents += advanced.TrafficEvents;
                result.MaxStopSeamCallsInUnit = Math.Max(result.MaxStopSeamCallsInUnit, (int)(stops.Calls - stopCalls));
                result.MaxRouteCallsInUnit = Math.Max(result.MaxRouteCallsInUnit, (int)(planner.Calls - routeCalls));
                if (unit >= 30) result.UnitSpikes30++;
                if (unit > result.UnitMax)
                {
                    result.UnitMax = unit; result.MaxUnitMinute = state.Minute;
                    result.MaxUnitKind = advanced.TrafficEvents > 0 ? "traffic" : advanced.FoodContinuationEvents > 0 ? "food-continuation"
                        : advanced.CitizenEvents > 0 ? "citizen" : "other";
                }
                double elapsed = (Stopwatch.GetTimestamp() - started) * ms;
                if (advanced.CaughtUp || citizens >= budget.CitizenEvents || elapsed >= budget.Milliseconds) break;
            }
            long syncStarted = Stopwatch.GetTimestamp();
            city.SynchronizeSnapshot();
            long ended = Stopwatch.GetTimestamp();
            result.SyncMax = Math.Max(result.SyncMax, (ended - syncStarted) * ms);
            double frame = (ended - started) * ms;
            frames.Add(frame);
            if (frame >= 30) result.FrameSpikes30++;
            if (frame >= result.FrameMax)
            { result.FrameMax = frame; result.MaxFrameMinute = state.Minute; result.CollectionsInMaxFrame = GC.CollectionCount(0) - collections; }
            result.MaxUnitsInFrame = Math.Max(result.MaxUnitsInFrame, units);
            result.PeakVehicles = Math.Max(result.PeakVehicles, city.Traffic?.VehicleCount ?? 0);
            if (frames.Count % 600 == 0)
                result.PeakTravelling = Math.Max(result.PeakTravelling, state.Citizens.Count(c => c.Phase == CitizenPhase.Travelling));
        }
        result.Frames = frames.Count;
        frames.Sort();
        result.FrameP50 = frames[frames.Count / 2];
        result.FrameP95 = frames[(int)((frames.Count - 1) * .95)];
        result.FrameP99 = frames[(int)((frames.Count - 1) * .99)];
        result.Purchases = state.Food!.Residents.Sum(m => m.Purchases);
        result.CompletedJourneys = state.Citizens.Sum(c => c.CompletedJourneys);
        result.Stops = city.OptionalStopChances.Realised;
        result.StopSeamCalls = stops.Calls; result.RouteCalls = planner.Calls;
        result.SaveSha256 = Sha(DevelopmentChain.Write(state));
        return result;
    }

    /// <summary>The same city advanced without frame slicing, for the cadence-independence hash.</summary>
    public static string RunUninterrupted(double from, double to, bool nativePrices = false)
    {
        var state = Create();
        var city = Attach(state, new Planner(), new Market(nativePrices), new Stops());
        while (!city.AdvanceTo(from, 256).CaughtUp) { }
        while (!city.AdvanceTo(to, 256).CaughtUp) { }
        return Sha(DevelopmentChain.Write(state));
    }

    public static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
