using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.MovementEquivalence;

public sealed class MovementMarket : IFoodMarket, IFoodSaleEvidence
{
    public double Clock;
    public double EarliestMinute => Math.Floor(Clock / 60) * 60;
    public readonly List<string> Sales = new();
    public readonly List<string> SaleOwners = new();
    public long RevenueUnits, Refusals;
    private readonly HashSet<string> paid = new(StringComparer.Ordinal);
    public static readonly (string Product, int Cents)[] Menu = {
        (FoodProducts.Burger, 800), (FoodProducts.Croissant, 300), (FoodProducts.HotDog, 500) };

    public IReadOnlyList<FoodBusiness> Read(double minute) => new[] { new FoodBusiness {
        Id = MovementFixture.Business, PlaceId = MovementFixture.ShopPlace, Name = "Own shop", MealsPerHour = MovementFixture.MealsPerHour,
        CustomerServicePercent = 50,
        Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = MovementFixture.OpenMinute, ToMinute = MovementFixture.CloseMinute }).ToList(),
        Offers = Menu.Select(m => new FoodOffer { ProductId = m.Product, Price = Money.FromCents(m.Cents), Units = 100000 }).ToList() } };

    public FoodSale Buy(ResidentMeal meal, double at)
    {
        if (!(at >= EarliestMinute && at < EarliestMinute + 60)) throw new HarnessViolation("historical native checkout at " + at);
        int minute = (int)(at % 1440);
        var offer = Menu.FirstOrDefault(m => m.Product == meal.ProductId);
        if (minute < MovementFixture.OpenMinute || minute >= MovementFixture.CloseMinute) { Refusals++; return new FoodSale(false, "Closed"); }
        if (offer.Product == null || Money.FromCents(offer.Cents) != meal.Price) { Refusals++; return new FoodSale(false, "Timestamped price changed"); }
        if (!paid.Add(meal.IntentId)) throw new HarnessViolation("duplicate native order " + meal.IntentId);
        RevenueUnits += meal.Price.Units;
        Sales.Add(meal.IntentId + "@" + at.ToString("R", CultureInfo.InvariantCulture) + ":" + meal.Price.Units);
        SaleOwners.Add(meal.ResidentId);
        return new FoodSale(true, "Native double confirmed");
    }
    public bool IsConfirmedPaid(ResidentMeal meal) => paid.Contains(meal.IntentId);
    public MovementMarket Copy()
    {
        var copy = new MovementMarket { Clock = Clock, RevenueUnits = RevenueUnits, Refusals = Refusals };
        copy.Sales.AddRange(Sales);
        copy.SaleOwners.AddRange(SaleOwners);
        copy.paid.UnionWith(paid);
        return copy;
    }
}

public sealed class HarnessViolation : Exception { public HarnessViolation(string message) : base(message) { } }

public sealed class GridPlanner : ICitizenRoutePlanner, ITrafficRoutePlanner, IFoodWalkingPace
{
    public const int Size = 25;
    public const double Spacing = 12;
    public const string Native = "f2af2af2af2af2af2af2af2af2af2af2af2af2af2af2af2af2af2af2af2af2a0";
    public const double ClockRate = 1.0 / 3;
    public const double WalkMetresPerMinute = 2 / ClockRate;
    public const double CarMetresPerMinute = 10 / ClockRate;
    public const double MinimumCarMetres = 96;
    public const int SignalEvery = 4;
    public TrafficNetwork? TrafficNetwork { get; }
    public double WalkingMetresPerMinute => WalkMetresPerMinute;

    public GridPlanner()
    {
        var nodes = new List<TrafficNetworkNode>();
        for (int x = 0; x < Size; x++)
            for (int z = 0; z < Size; z++)
            {
                var successors = new List<int>();
                if (x > 0) successors.Add(Id(x - 1, z));
                if (x < Size - 1) successors.Add(Id(x + 1, z));
                if (z > 0) successors.Add(Id(x, z - 1));
                if (z < Size - 1) successors.Add(Id(x, z + 1));
                var node = new TrafficNetworkNode { Id = Id(x, z), Position = Node(x, z), KilometresPerHour = 36, AllowsCar = true,
                    Boundary = true, StructuralContract = "road", Successors = successors.ToArray() };
                if (x % SignalEvery == 0 && z % SignalEvery == 0)
                    node.Signal = new TrafficSignalSchedule { CycleMinutes = 2, GreenStartMinutes = 0, GreenDurationMinutes = 1,
                        PhaseOffsetMinutes = (x + z) / SignalEvery % 2 };
                nodes.Add(node);
            }
        TrafficNetwork = new TrafficNetwork(Native, nodes, ClockRate);
    }
    public static int Id(int x, int z) => x * Size + z;
    public static Position Node(int x, int z) => new Position(x * Spacing, 0, z * Spacing);
    public static (int X, int Z) Nearest(Position p) => (Math.Max(0, Math.Min(Size - 1, (int)Math.Round(p.X / Spacing))),
        Math.Max(0, Math.Min(Size - 1, (int)Math.Round(p.Z / Spacing))));
    public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
    public bool IsTrafficLinkClosed(int linkId) => false;

    public long Requests, CarRoutes;
    public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences) => Plan(from, to, null);
    public Route? Plan(Place from, Place to, double departureMinute, CitizenState citizen) => Plan(from, to, citizen);

    private Route? Plan(Place from, Place to, CitizenState? citizen)
    {
        Requests++;
        var route = new Route();
        var start = Nearest(from.Position); var end = Nearest(to.Position);
        var car = citizen?.Car;
        if (car != null && citizen!.Preferences.OwnsCar && Position.Distance(from.Position, car.ParkedPosition) < 20
            && (Math.Abs(end.X - start.X) + Math.Abs(end.Z - start.Z)) * Spacing >= MinimumCarMetres)
        {
            var parked = Nearest(car.ParkedPosition);
            Walk(route, from.Position, car.ParkedPosition);
            Pause(route, car.ParkedPosition, TransportComposition.CarPreparationMinutes);
            Connector(route, car.ParkedPosition, Node(parked.X, parked.Z));
            Streets(route, parked, end, TravelMode.Car);
            var curb = Kerb(Node(end.X, end.Z), to.Position);
            Connector(route, Node(end.X, end.Z), curb);
            Pause(route, curb, TransportComposition.CarParkingMinutes);
            Walk(route, curb, to.Position);
            CarRoutes++;
            return route;
        }
        Walk(route, from.Position, Node(start.X, start.Z));
        Streets(route, start, end, TravelMode.Walk);
        Walk(route, Node(end.X, end.Z), to.Position);
        return route.Legs.Count == 0 ? null : route;
    }

    private static void Streets(Route route, (int X, int Z) from, (int X, int Z) to, TravelMode mode)
    {
        int x = from.X, z = from.Z;
        while (x != to.X || z != to.Z)
        {
            int nx = x != to.X ? x + Math.Sign(to.X - x) : x;
            int nz = x != to.X ? z : z + Math.Sign(to.Z - z);
            if (mode == TravelMode.Car)
                route.Legs.Add(new RouteLeg { From = Node(x, z), To = Node(nx, nz), Mode = TravelMode.Car, Minutes = Spacing / CarMetresPerMinute,
                    ServiceId = "native:road:v1:" + Native + ":" + Id(x, z).ToString(CultureInfo.InvariantCulture) + ":" + Id(nx, nz).ToString(CultureInfo.InvariantCulture) });
            else Walk(route, Node(x, z), Node(nx, nz));
            x = nx; z = nz;
        }
    }

    private static void Connector(Route route, Position from, Position to)
    {
        double metres = Position.Distance(from, to);
        if (metres > 1e-6) route.Legs.Add(new RouteLeg { From = from, To = to, Mode = TravelMode.Car, Minutes = metres / WalkMetresPerMinute });
    }
    private static void Pause(Route route, Position at, double minutes)
        => route.Legs.Add(new RouteLeg { From = at, To = at, Mode = TravelMode.Car, Minutes = minutes });
    private static Position Kerb(Position road, Position destination)
    {
        double metres = Position.Distance(road, destination);
        return metres <= 1e-6 ? road : Position.Lerp(road, destination, Math.Min(1, 6 / metres));
    }

    private static void Walk(Route route, Position from, Position to)
    {
        double metres = Position.Distance(from, to);
        if (metres > 1e-6) route.Legs.Add(new RouteLeg { From = from, To = to, Mode = TravelMode.Walk, Minutes = metres / WalkMetresPerMinute });
    }
}

public static class MovementFixture
{
    public const int Residents = 957;
    public const int RepresentationFactor = 2;
    public const int Homes = 120, Workplaces = 10, Leisure = 4;
    public const double StartMinute = 1440;
    public const int OpenMinute = 7 * 60, CloseMinute = 22 * 60, MealsPerHour = 60;
    public const string Business = "b-shop", ShopPlace = "shop";
    public static readonly Position DoorOffset = new Position(3, 0, 3);
    public static Position ShopPosition => Door(12, 12);
    public static Position Door(int x, int z) { var n = GridPlanner.Node(x, z); return new Position(n.X + DoorOffset.X, n.Y, n.Z + DoorOffset.Z); }

    public static readonly PortalStation[] Portals = {
        new("subway:west", Door(10, 12)), new("subway:east", Door(14, 12)), new("subway:north", Door(12, 15)) };

    public static CityState Build(int seed, int residents, bool carOwners = true, int representationFactor = RepresentationFactor, bool visitors = false)
    {
        uint draw = (uint)(seed * 2654435761u + 12345u);
        int Pick(int n) { draw = draw * 1664525u + 1013904223u; return (int)((draw >> 8) % (uint)n); }
        var places = new List<Place> { new Place { Id = ShopPlace, Name = "Own shop", Kind = PlaceKind.Shop, Position = ShopPosition,
            OpenMinute = OpenMinute, CloseMinute = CloseMinute } };
        int perHome = (residents + Homes - 1) / Homes;
        for (int i = 0; i < Homes; i++)
            places.Add(new Place { Id = "home" + i.ToString("D3", CultureInfo.InvariantCulture), Name = "Home " + i, Kind = PlaceKind.Home,
                Capacity = perHome + 1, Position = Door(Pick(GridPlanner.Size), Pick(GridPlanner.Size)) });
        for (int i = 0; i < Workplaces; i++)
            places.Add(new Place { Id = "work" + i, Name = "Work " + i, Kind = PlaceKind.Work, Capacity = residents / Workplaces + 2,
                Position = Door(Pick(GridPlanner.Size), Pick(GridPlanner.Size)), OpenMinute = 0, CloseMinute = 1440 });
        for (int i = 0; i < Leisure; i++)
            places.Add(new Place { Id = "leisure" + i, Name = "Leisure " + i, Kind = PlaceKind.Leisure, Capacity = residents,
                Position = Door(Pick(GridPlanner.Size), Pick(GridPlanner.Size)), OpenMinute = 0, CloseMinute = 1440, AttractionWeight = 1 });
        var state = Population.Create(Enumerable.Range(0, residents).Select(n => new CitizenIdentity {
            Id = "r" + n.ToString("D4", CultureInfo.InvariantCulture), Name = "Resident " + n, Persona = Persona.Foodie, Age = 30,
            AppearanceSeed = n + 1 }).ToArray(), places, "movement-equivalence", "0.40.4", seed, StartMinute);
        for (int i = 0; i < state.Citizens.Count; i++)
        {
            var person = state.Citizens[i];
            if (!carOwners || i % 2 != 0) continue;
            var home = state.Places.First(p => p.Id == person.HomeId);
            var corner = GridPlanner.Nearest(home.Position);
            person.Preferences.OwnsCar = true;
            person.Car = new CarState { Id = person.Identity.Id + ":car", ModelId = "harness", ParkedPosition = GridPlanner.Node(corner.X, corner.Z) };
        }
        var products = MovementMarket.Menu.Select(m => m.Product).OrderBy(p => p, StringComparer.Ordinal).ToList();
        if (visitors) PortalPlaces.Add(state, Portals);
        state.Food = new FoodCommerceState { RepresentationFactor = representationFactor, MealRhythmVersion = MealRhythm.PolicyVersion,
            Shops = new List<FoodScope> { new FoodScope { PlaceId = ShopPlace, BusinessId = Business,
                ProductId = products[0], AdditionalProductIds = products.Skip(1).ToList() } } };
        return state;
    }

    public static CitySimulator Attach(CityState state, MovementMarket market, GridPlanner planner, bool fresh)
    {
        var city = new CitySimulator(state, planner);
        city.AttachFoodMarket(market);
        if (fresh)
        {
            city.EnableHouseholdFinances();
            state.Food!.ChoiceStartsDay = state.Food.AwarenessStartsDay = 1;
        }
        return city;
    }
}
