using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>
/// The 28 October core identity change replaced per-call string keys with cached hash states, cached
/// opening windows per place and decoded road tags once per leg. Nothing a resident does may change:
/// these tests compare the production helpers with the pre-change copies in CoreIdentityReference on a
/// simulated city, and compare whole runs with and without the transient caches.
/// </summary>
public sealed class CoreIdentityTests
{
    private const int Seed = 20261028;
    private const string Native = "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00";
    private const int Size = 21;
    private const double Spacing = 50;

    private static int NodeId(int x, int z) => x * Size + z;
    private static Position Node(int x, int z) => new(x * Spacing, 0, z * Spacing);
    private static string Tag(int from, int to, string kind = "v1", string topology = Native)
        => "native:road:" + kind + ":" + topology + ":" + from.ToString(CultureInfo.InvariantCulture) + ":" + to.ToString(CultureInfo.InvariantCulture);

    private static TrafficNetwork Grid(string topology = Native)
    {
        var nodes = new List<TrafficNetworkNode>();
        for (int x = 0; x < Size; x++)
            for (int z = 0; z < Size; z++)
            {
                var next = new List<int>();
                if (x > 0) next.Add(NodeId(x - 1, z));
                if (x < Size - 1) next.Add(NodeId(x + 1, z));
                if (z > 0) next.Add(NodeId(x, z - 1));
                if (z < Size - 1) next.Add(NodeId(x, z + 1));
                nodes.Add(new TrafficNetworkNode { Id = NodeId(x, z), Position = Node(x, z), KilometresPerHour = 36, AllowsCar = true,
                    Boundary = true, StructuralContract = "road", Successors = next.ToArray() });
            }
        // 36 km/h at one game minute per real minute, so a 50 m section takes 1/12 minute like the planned legs.
        return new TrafficNetwork(topology, nodes, 1.0 / 60);
    }

    /// <summary>Grid driving with native tags for car owners, walking otherwise, one refusal in thirteen; pure in the request.</summary>
    private sealed class Planner : ICitizenRoutePlanner, ICommuteTimingPlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; } = Grid();
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => Plan(from, to, minute, null, preferences);
        public Route? Plan(Place from, Place to, double minute, CitizenState citizen) => Plan(from, to, minute, citizen, citizen.Preferences);
        private static (int X, int Z) Nearest(Position p) => (Math.Max(0, Math.Min(Size - 1, (int)Math.Round(p.X / Spacing))),
            Math.Max(0, Math.Min(Size - 1, (int)Math.Round(p.Z / Spacing))));
        private static Route? Plan(Place from, Place to, double minute, CitizenState? citizen, TravelPreferences preferences)
        {
            if (StableRandom.Hash(from.Id + ">" + to.Id + "@" + minute.ToString("R", CultureInfo.InvariantCulture), 3) % 13 == 0) return null;
            var route = new Route();
            if (citizen != null && preferences.OwnsCar && Position.Distance(from.Position, to.Position) > 300)
            {
                if (citizen.Car == null && citizen.Phase == CitizenPhase.AtPlace && citizen.PlaceId == citizen.HomeId && from.Id == citizen.HomeId)
                {
                    var home = Nearest(from.Position);
                    citizen.Car = new CarState { Id = citizen.Identity.Id + ":car", ModelId = "test", ParkedPosition = Node(home.X, home.Z) };
                }
                if (citizen.Car != null && Position.Distance(from.Position, citizen.Car.ParkedPosition) < 200)
                {
                    var start = Nearest(citizen.Car.ParkedPosition); var end = Nearest(to.Position);
                    if (Position.Distance(Node(start.X, start.Z), citizen.Car.ParkedPosition) < 1e-9 && start != end)
                    {
                        Walk(route, from.Position, citizen.Car.ParkedPosition);
                        int x = start.X, z = start.Z;
                        while (x != end.X || z != end.Z)
                        {
                            int nx = x != end.X ? x + Math.Sign(end.X - x) : x, nz = x != end.X ? z : z + Math.Sign(end.Z - z);
                            route.Legs.Add(new RouteLeg { From = Node(x, z), To = Node(nx, nz), Mode = TravelMode.Car, Minutes = Spacing / 600.0, ServiceId = Tag(NodeId(x, z), NodeId(nx, nz)) });
                            x = nx; z = nz;
                        }
                        Walk(route, Node(end.X, end.Z), to.Position);
                        return route;
                    }
                }
            }
            Walk(route, from.Position, to.Position);
            return route.Legs.Count == 0 ? null : route;
        }
        private static void Walk(Route route, Position from, Position to)
        {
            double metres = Position.Distance(from, to);
            if (metres > 1e-6) route.Legs.Add(new RouteLeg { From = from, To = to, Mode = TravelMode.Walk, Minutes = metres / 80.0 });
        }
    }

    private static List<Place> Places()
    {
        uint state = 977;
        double Next() { state = state * 1664525u + 1013904223u; return (state >> 8) / 16777216.0; }
        int Pick(int n) => (int)(Next() * n);
        var places = new List<Place>();
        double extent = (Size - 1) * Spacing;
        void Add(string prefix, PlaceKind kind, int count, int capacity)
        {
            for (int i = 0; i < count; i++)
            {
                var p = new Place
                {
                    Id = prefix + "-" + i.ToString("D3", CultureInfo.InvariantCulture), Name = prefix + " " + i, Kind = kind, Capacity = capacity,
                    Position = new Position(Next() * extent, 0, Next() * extent), AttractionWeight = 0.5 + Next() * 3,
                    NativeTrafficIndex = Pick(4) == 0 ? -1 : Pick(120), District = i % 2 == 0 ? "a" : "b"
                };
                if (kind != PlaceKind.Home)
                {
                    int style = Pick(5);
                    if (style == 0) { p.OpenMinute = 0; p.CloseMinute = 1440; }
                    else if (style == 1) { p.OpenMinute = 420 + Pick(8) * 30; p.CloseMinute = 1020 + Pick(10) * 30; }
                    else if (style == 2) { p.OpenMinute = 1080; p.CloseMinute = 120 + Pick(4) * 30; }
                    else
                    {
                        p.OpenMinute = 0; p.CloseMinute = 0;
                        for (int day = 0; day < 7; day++)
                        {
                            if (Pick(6) == 0) continue;
                            int from = 360 + Pick(10) * 30;
                            int to = style == 3 ? from + 360 + Pick(10) * 30 : (from + 900 + Pick(8) * 30) % 1440;
                            p.WeeklyHours.Add(new OpeningWindow { Day = day, FromMinute = from, ToMinute = Math.Min(to, 1439) });
                            if (Pick(3) == 0) p.WeeklyHours.Add(new OpeningWindow { Day = day, FromMinute = 1200, ToMinute = 1380 });
                        }
                    }
                }
                places.Add(p);
            }
        }
        Add("home", PlaceKind.Home, 150, 3);
        Add("work", PlaceKind.Work, 60, 12);
        Add("study", PlaceKind.Study, 8, 40);
        Add("shop", PlaceKind.Shop, 90, 30);
        Add("leisure", PlaceKind.Leisure, 90, 30);
        return places;
    }

    private static CityState City(int residents = 320)
    {
        var identities = Enumerable.Range(0, residents).Select(i => new CitizenIdentity
        {
            Id = "rlo:identity:" + i.ToString("D4", CultureInfo.InvariantCulture), Name = "Resident " + i, Age = i % 8 == 4 ? 70 : i % 8 == 3 ? 20 : 35,
            Female = i % 2 == 0, District = i % 3 == 0 ? "a" : "b", Persona = (Persona)(i % 8), AppearanceSeed = i + 1
        });
        var city = Population.Create(identities, Places(), "core-identity-test", "0.40.4", Seed, 1440);
        // Some residents start unemployed so the daily employment check probes workplaces.
        for (int i = 0; i < city.Citizens.Count; i += 6) city.Citizens[i].DayPlaceId = "";
        return city;
    }

    private static string Observation(string id, CitizenState c, double minute) => id + "@" + minute.ToString("R", CultureInfo.InvariantCulture)
        + ":" + c.Phase + ":" + c.Activity + ":" + c.PlaceId + ">" + c.DestinationId + ":" + c.NextEventMinute.ToString("R", CultureInfo.InvariantCulture)
        + ":" + c.ActivityUntilMinute.ToString("R", CultureInfo.InvariantCulture) + ":" + (c.Journey?.Legs.Count ?? -1);

    private static void ClearTransientCaches(CityState state)
    {
        foreach (var person in state.Citizens) person.RoutineKeys = null;
        foreach (var place in state.Places) place.OpeningCache = null;
    }

    private static void AssertRoutineMatchesReference(CityState state, CitizenState person, double now, IReadOnlyDictionary<string, Place> places)
    {
        int seed = state.Seed;
        places.TryGetValue(person.HomeId, out var home);
        var minutes = new List<double> { now, now + 17, now + 333 };
        if (person.NextEventMinute < 1e8) minutes.Add(person.NextEventMinute);
        if (person.ActivityUntilMinute > 0 && person.ActivityUntilMinute < 1e8) minutes.Add(person.ActivityUntilMinute - 0.001);
        foreach (double minute in minutes)
        {
            var expected = CoreIdentityReference.At(person, seed, minute, state.Places);
            Assert.Equal(expected, DailyRoutine.At(person, seed, minute, state.Places));
            Assert.Equal(expected, DailyRoutine.At(person, seed, minute, state.Places, home));
            Assert.Equal(CoreIdentityReference.At(person, seed, minute), DailyRoutine.At(person, seed, minute));
            Assert.Equal(CoreIdentityReference.WorkOnDay(person, seed, minute), DailyRoutine.WorkOnDay(person, seed, minute));
            var work = CoreIdentityReference.NextWork(person, seed, minute);
            Assert.Equal(work, DailyRoutine.NextWork(person, seed, minute));
            if (work.HasValue)
                Assert.Equal(CoreIdentityReference.CommutePreparationAt(person, seed, work.Value, 12.5),
                    DailyRoutine.CommutePreparationAt(person, seed, work.Value, 12.5));
            int day = (int)Math.Floor(minute / 1440);
            Assert.Equal(CoreIdentityReference.OnDay(person, seed, day), DailyRhythm.OnDay(person, seed, day));
            Assert.Equal(CoreIdentityReference.OnDay(person, seed, day - 1), DailyRhythm.OnDay(person, seed, day - 1));
        }
        Assert.Equal(CoreIdentityReference.NextHomeEventDraw(person), RoutineKeys.EmploymentTimeFor(person) % 240);
        foreach (var id in new[] { person.PlaceId, person.DestinationId, person.ShopId, person.LeisureId })
        {
            if (!places.TryGetValue(id, out var place)) continue;
            foreach (double minute in new[] { now, now + 61, now + 1500 })
            {
                Assert.Equal(CoreIdentityReference.NextClose(place, minute), DailyRoutine.NextClose(place, minute));
                Assert.Equal(CoreIdentityReference.NextOpen(place, minute, minute + 700), DailyRoutine.NextOpen(place, minute, minute + 700));
                Assert.Equal(CoreIdentityReference.Open(place, minute), DailyRoutine.Open(place, minute));
            }
        }
    }

    [Fact]
    public void IncrementalHashEqualsTheConcatenatedKey()
    {
        var prefixes = new[] { "", "a", "rlo:identity:0001:shop:", "pc:17:quiet-day:", "é中:" };
        var numbers = new[] { 0, 1, 9, 10, 99, 100, 12345, -1, -10, -2147483647, int.MinValue, int.MaxValue, 1000000000 };
        foreach (int seed in new[] { 0, 1, -7, Seed, int.MinValue })
            foreach (var prefix in prefixes)
            {
                uint state = StableRandom.Append(StableRandom.Begin(seed), prefix);
                Assert.Equal(StableRandom.Hash(prefix, seed), StableRandom.Finish(state));
                Assert.Equal(StableRandom.Hash(prefix + ":", seed), StableRandom.Finish(StableRandom.Append(state, ':')));
                foreach (int n in numbers)
                    Assert.Equal(StableRandom.Hash(prefix + n.ToString(CultureInfo.InvariantCulture), seed), RoutineKeys.Draw(state, n));
                for (int n = -3000; n <= 3000; n += 7)
                    Assert.Equal(StableRandom.Hash(prefix + n.ToString(CultureInfo.InvariantCulture), seed), RoutineKeys.Draw(state, n));
            }
    }

    [Fact]
    public void RoutineHelpersMatchTheReferenceAtEveryEventOfASimulatedCity()
    {
        var state = City();
        var places = state.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var city = new CitySimulator(state, new Planner());
        int checks = 0, leisure = 0, shop = 0;
        city.ActivityObserved += (_, person, minute) =>
        {
            if (person.Activity == Activity.Leisure || person.DestinationActivity == Activity.Leisure) leisure++;
            if (person.Activity == Activity.Shop || person.DestinationActivity == Activity.Shop) shop++;
            // Every few events also run on a resident without its cache, so both paths meet the reference.
            if (checks % 3 == 0) person.RoutineKeys = null;
            AssertRoutineMatchesReference(city.State, person, minute, places);
            checks++;
        };
        for (int day = 1; day <= 3; day++)
        {
            double target = 1440 * (day + 1.0);
            while (!city.IsCaughtUpTo(target)) city.AdvanceFrameSlice(target, 4);
        }
        Assert.True(checks > 5000, "too few events: " + checks);
        Assert.True(leisure > 200 && shop > 200, "too few optional activities: " + leisure + " leisure, " + shop + " shop");
    }

    [Fact]
    public void WholeRunsAgreeWithAndWithoutTransientCachesAndAcrossCadence()
    {
        // A: one event per frame, caches warm. B: three events per frame with every transient cache dropped
        // before each frame, so each value is recomputed from the strings. C: AdvanceTo per day. The routine
        // helpers equal the reference (above), so equal runs mean the caches changed nothing.
        var a = new CitySimulator(City(), new Planner()); var b = new CitySimulator(City(), new Planner()); var c = new CitySimulator(City(), new Planner());
        var ea = new List<string>(); var eb = new List<string>(); var ec = new List<string>();
        a.ActivityObserved += (id, p, m) => ea.Add(Observation(id, p, m));
        b.ActivityObserved += (id, p, m) => eb.Add(Observation(id, p, m));
        c.ActivityObserved += (id, p, m) => ec.Add(Observation(id, p, m));
        int carLegs = 0;
        for (int day = 1; day <= 4; day++)
        {
            double target = 1440 * (day + 1.0);
            while (!a.IsCaughtUpTo(target)) a.AdvanceFrameSlice(target, 1);
            while (!b.IsCaughtUpTo(target)) { ClearTransientCaches(b.State); b.AdvanceFrameSlice(target, 3); }
            c.AdvanceTo(target);
            carLegs += a.State.Citizens.Count(p => p.Journey?.Legs.Any(l => l.Mode == TravelMode.Car) == true);
            string expected = SaveCodec.Write(c.State);
            Assert.Equal(expected, SaveCodec.Write(a.State));
            Assert.Equal(expected, SaveCodec.Write(b.State));
            if (day == 2)
            {
                // Reload: transient fields are rebuilt from the loaded strings with the same results.
                var read = SaveCodec.Read(expected);
                Assert.Equal(SaveReadStatus.Loaded, read.Status);
                b = new CitySimulator(read.State!, new Planner());
                b.ActivityObserved += (id, p, m) => eb.Add(Observation(id, p, m));
            }
        }
        Assert.Equal(ec, ea);
        Assert.Equal(ec, eb);
        Assert.True(ea.Count > 8000, "too few events: " + ea.Count);
        Assert.Contains(a.State.Citizens, p => p.Car != null && p.CompletedJourneys > 0);
    }

    [Fact]
    public void DestinationChoiceMatchesTheReference()
    {
        var places = Places();
        var origins = places.Where(p => p.Kind == PlaceKind.Home).Take(40).ToList();
        foreach (var origin in origins)
            foreach (var kind in new[] { PlaceKind.Leisure, PlaceKind.Shop, PlaceKind.Work })
            {
                string key = origin.Id + ":" + kind;
                Assert.Equal(CoreIdentityReference.Choose(places, origin, kind, key, Seed), DestinationChoice.Choose(places, origin, kind, key, Seed));
                Func<Place, bool> eligible = p => StableRandom.Hash(p.Id, 1) % 3 != 0;
                Assert.Equal(CoreIdentityReference.Choose(places, origin, kind, key, Seed, eligible), DestinationChoice.Choose(places, origin, kind, key, Seed, eligible));
                Assert.Equal(CoreIdentityReference.Choose(places, origin, kind, key, Seed), DestinationChoice.Choose(places, origin, kind, DestinationChoice.KeyState(key, Seed)));
                Assert.Equal(CoreIdentityReference.Shortlist(places, origin, kind, key, Seed).Select(p => p.Id),
                    DestinationChoice.Shortlist(places, origin, kind, key, Seed).Select(p => p.Id));
            }
        Assert.Equal(CoreIdentityReference.Choose(places, origins[0], PlaceKind.Shop, null!, Seed), DestinationChoice.Choose(places, origins[0], PlaceKind.Shop, null!, Seed));
    }

    [Fact]
    public void OpeningCacheFollowsEveryWriterOfThePlaceHours()
    {
        var place = new Place { Id = "venue", Kind = PlaceKind.Shop, OpenMinute = 480, CloseMinute = 1200 };
        void Check()
        {
            for (double minute = 1440 * 3; minute < 1440 * 12; minute += 47)
            {
                Assert.Equal(CoreIdentityReference.NextClose(place, minute), DailyRoutine.NextClose(place, minute));
                Assert.Equal(CoreIdentityReference.NextOpen(place, minute, minute + 900), DailyRoutine.NextOpen(place, minute, minute + 900));
                // Alternate between two days, then return, which the two-day cache must answer correctly.
                Assert.Equal(CoreIdentityReference.NextClose(place, minute + 1440), DailyRoutine.NextClose(place, minute + 1440));
            }
        }
        Check();
        place.CloseMinute = 1320; Check();
        place.OpenMinute = 1320; place.CloseMinute = 60; Check();
        place.WeeklyHours.Add(new OpeningWindow { Day = 2, FromMinute = 600, ToMinute = 900 }); Check();
        place.WeeklyHours.Add(new OpeningWindow { Day = 5, FromMinute = 1300, ToMinute = 200 }); Check();
        place.WeeklyHours[0].FromMinute = 540; Check();
        place.WeeklyHours[1].Day = 6; Check();
        place.WeeklyHours = new List<OpeningWindow> { new() { Day = 0, FromMinute = 0, ToMinute = 0 }, new() { Day = 3, FromMinute = 0, ToMinute = 1439 } }; Check();
        place.WeeklyHours.RemoveAt(1); Check();
        place.WeeklyHours.Clear(); Check();
        for (int day = 0; day < 7; day++) place.WeeklyHours.Add(new OpeningWindow { Day = day, FromMinute = 0, ToMinute = 1440 });
        Check();
        Assert.Equal(double.PositiveInfinity, DailyRoutine.NextClose(place, 1440 * 5));
    }

    [Fact]
    public void RoadTagDecodingMatchesSplitParsing()
    {
        string other = Native.Replace('c', 'd');
        var corpus = new List<string?>
        {
            null, "", "native", "native:road", "native:road:", "native:road:v1", "native:subway", "s1",
            Tag(1, 2), Tag(1, 2, "fragment1"), Tag(1, 2, "v2"), Tag(1, 2, "V1"), Tag(1, 1), Tag(1, 1, "fragment1"), Tag(0, 0),
            Tag(1, 2, "v1", other), Tag(1, 2, "fragment1", other), Tag(1, 2, "v1", ""), Tag(1, 2, "v1", "x:y"),
            "native:road:v1:" + Native + ":007:0", "native:road:v1:" + Native + ":-1:2", "native:road:v1:" + Native + ":+1:2",
            "native:road:v1:" + Native + ": 1:2", "native:road:v1:" + Native + ":1 :2", "native:road:v1:" + Native + ":1:",
            "native:road:v1:" + Native + "::2", "native:road:v1:" + Native + ":1:2:", "native:road:v1:" + Native + ":1:2:3",
            "native:road:v1:" + Native + ":2147483647:2147483646", "native:road:v1:" + Native + ":2147483648:1",
            "native:road:v1:" + Native + ":99999999999:1", "native:road:v1:" + Native + ":١:2", "native:road:v1:" + Native + ":1\u0000:2",
            "native:road:v1:" + Native + ":1:2\u0000", "native:road:v1:" + Native + ":0x1:2", "native:road:v1:" + Native + ":1.0:2",
            "native:road::" + Native + ":1:2", "native:Road:v1:" + Native + ":1:2", "Native:road:v1:" + Native + ":1:2",
            ":native:road:v1:" + Native + ":1:2", "native:road:fragment1:" + Native + ":4294967296:3"
        };
        var random = new Random(5);
        const string alphabet = "native:roadv1fragment-+09 \u0000c";
        for (int i = 0; i < 4000; i++)
        {
            var chars = Tag(random.Next(0, 30), random.Next(0, 30), random.Next(2) == 0 ? "v1" : "fragment1", random.Next(3) == 0 ? other : Native).ToCharArray();
            int edits = random.Next(0, 3);
            for (int e = 0; e < edits; e++) chars[random.Next(chars.Length)] = alphabet[random.Next(alphabet.Length)];
            corpus.Add(new string(chars));
        }
        foreach (var service in corpus)
        {
            var leg = new RouteLeg { Mode = TravelMode.Car, ServiceId = service! };
            var tag = leg.RoadTag;
            foreach (var topology in new[] { Native, other, "" })
            {
                bool expected = CoreIdentityReference.NetworkTag(service, topology, out int from, out int to, out bool fragment);
                bool actual = service != null && tag.IsTopology(service, topology);
                Assert.True(expected == actual, "network tag " + service + " / " + topology);
                if (expected) { Assert.Equal(from, tag.From); Assert.Equal(to, tag.To); Assert.Equal(fragment, tag.Kind == RoadTagKind.Fragment1); }
                expected = CoreIdentityReference.RoadRoutesTag(service, topology, out from, out to);
                Assert.Equal(expected, service != null && tag.Kind == RoadTagKind.V1 && tag.IsTopology(service, topology));
                if (expected) { Assert.Equal(from, tag.From); Assert.Equal(to, tag.To); }
                if (service == null) continue;
                expected = CoreIdentityReference.RoadRoutesFragment(service, topology, out from, out to);
                Assert.Equal(expected, tag.Kind == RoadTagKind.Fragment1 && tag.IsTopology(service, topology) && tag.From != tag.To);
                if (expected) { Assert.Equal(from, tag.From); Assert.Equal(to, tag.To); }
            }
        }
    }

    private static Route Drive(string topology, int legs)
    {
        var route = new Route();
        route.Legs.Add(new RouteLeg { From = new Position(-20, 0, 0), To = Node(0, 0), Mode = TravelMode.Walk, Minutes = 0.25 });
        for (int i = 0; i < legs; i++)
            route.Legs.Add(new RouteLeg { From = Node(0, i), To = Node(0, i + 1), Mode = TravelMode.Car, Minutes = Spacing / 600.0, ServiceId = Tag(NodeId(0, i), NodeId(0, i + 1), "v1", topology) });
        route.Legs.Add(new RouteLeg { From = Node(0, legs), To = new Position(-20, 0, legs * Spacing), Mode = TravelMode.Walk, Minutes = 0.25 });
        return route;
    }

    [Fact]
    public void MappingUsesDecodedTagsAndStillRejectsAnotherTopology()
    {
        var network = Grid();
        var route = Drive(Native, 12);
        var spans = new List<TrafficNetwork.MappedSpan>();
        for (double progress = 0; progress <= route.Minutes; progress += route.Minutes / 37)
        {
            bool mapped = network.TryMapRemaining(route, progress, out var legs);
            Assert.Equal(mapped, network.TryMapRemainingSpans(route, progress, spans));
            Assert.Equal(legs.Count, spans.Count);
            for (int i = 0; i < legs.Count; i++)
            {
                Assert.Equal(legs[i].LinkId, spans[i].LinkId); Assert.Equal(legs[i].StartNominalMinutes, spans[i].StartNominalMinutes);
                Assert.Equal(legs[i].EndNominalMinutes, spans[i].EndNominalMinutes); Assert.Equal(legs[i].FreeFlowMinutes, spans[i].FreeFlowMinutes);
                Assert.Equal(legs[i].StartLinkFraction, spans[i].StartLinkFraction); Assert.Equal(legs[i].EndLinkFraction, spans[i].EndLinkFraction);
            }
        }
        Assert.True(network.TryMap(route, out var full)); Assert.Equal(12, full.Count);
        // A route planned on another snapshot does not map here, and maps again once its tags are rewritten.
        var foreign = Drive(Native.Replace('c', 'd'), 12);
        Assert.False(network.TryMap(foreign, out var none)); Assert.Empty(none);
        Assert.False(network.TryMapRemainingSpans(foreign, 0, spans)); Assert.Empty(spans);
        Assert.True(Grid(Native.Replace('c', 'd')).TryMap(foreign, out _));
        foreach (var leg in foreign.Legs.Where(l => l.Mode == TravelMode.Car))
            leg.ServiceId = leg.ServiceId.Replace(Native.Replace('c', 'd'), Native);
        Assert.True(network.TryMap(foreign, out var rewritten));
        Assert.Equal(full.Select(l => l.LinkId), rewritten.Select(l => l.LinkId));
        // A malformed tag still fails the whole mapping, as before.
        foreign.Legs[3].ServiceId = "native:road:v1:" + Native + ":x:1";
        Assert.False(network.TryMap(foreign, out _));
    }

    [Fact]
    public void TransientFieldsAreNeverSavedAndFollowTheirInputs()
    {
        var state = City(40);
        string before = SaveCodec.Write(state);
        foreach (var person in state.Citizens) DailyRoutine.At(person, state.Seed, 1440 * 3 + 1200, state.Places);
        foreach (var place in state.Places) DailyRoutine.NextClose(place, 1440 * 3 + 600);
        foreach (var person in state.Citizens) foreach (var leg in person.Journey?.Legs ?? new List<RouteLeg>()) _ = leg.RoadTag;
        Assert.All(state.Citizens, p => Assert.NotNull(p.RoutineKeys));
        Assert.Equal(before, SaveCodec.Write(state));
        Assert.Equal(before, SaveCodec.Write(SaveCodec.Read(before).State!));

        var person0 = state.Citizens[0];
        var keys = person0.RoutineKeys;
        var working = CitySimulator.CloneForEvent(person0);
        Assert.Same(keys, working.RoutineKeys);
        working.RoutineKeys = null;
        DailyRoutine.At(working, state.Seed, 1440 * 4);
        CitySimulator.CommitEvent(person0, working);
        Assert.Same(working.RoutineKeys, person0.RoutineKeys);

        // A different identity or seed is never answered from the cache.
        var other = new CitizenState { Identity = new CitizenIdentity { Id = person0.Identity.Id, Persona = person0.Identity.Persona },
            DayPlaceId = "work-001", ShopId = "shop-001", LeisureId = "leisure-001", HomeId = person0.HomeId };
        for (int n = 0; n < 4; n++)
        {
            other.Identity.Id = n % 2 == 0 ? "resident-" + n : person0.Identity.Id;
            int seed = n < 2 ? state.Seed : state.Seed + 1;
            for (double minute = 1440 * 2; minute < 1440 * 9; minute += 97)
            {
                Assert.Equal(CoreIdentityReference.At(other, seed, minute, state.Places), DailyRoutine.At(other, seed, minute, state.Places));
                Assert.Equal(CoreIdentityReference.NextHomeEventDraw(other), RoutineKeys.EmploymentTimeFor(other) % 240);
            }
        }
    }

    [Fact]
    public void WarmRoutineOpeningAndMappingQueriesDoNotAllocate()
    {
        var state = City(60);
        var places = state.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var network = Grid(); var route = Drive(Native, 40); var spans = new List<TrafficNetwork.MappedSpan>();
        void Queries(double day)
        {
            foreach (var person in state.Citizens)
            {
                places.TryGetValue(person.HomeId, out var home);
                for (double minute = day * 1440; minute < day * 1440 + 1440; minute += 30)
                {
                    DailyRoutine.At(person, state.Seed, minute, state.Places, home);
                    DailyRoutine.NextWork(person, state.Seed, minute);
                }
            }
            foreach (var place in state.Places)
                for (double minute = day * 1440; minute < day * 1440 + 1440; minute += 30)
                { DailyRoutine.NextClose(place, minute); DailyRoutine.NextOpen(place, minute, minute + 600); }
            for (int i = 0; i < 50; i++) network.TryMapRemainingSpans(route, route.Minutes * i / 50, spans);
        }
        Queries(3); // Builds each resident's keys, each place's opening windows and the span buffer.
        long before = GC.GetAllocatedBytesForCurrentThread();
        Queries(3);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
