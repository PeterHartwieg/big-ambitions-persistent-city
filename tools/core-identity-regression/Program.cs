// Deterministic multi-day city run for comparing two source trees (see compare.py and README.md).
// It uses only public Core and Persistence APIs that exist before and after the core identity change,
// so the same file builds against both. Output is one JSON object on stdout.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace CoreIdentityRegression
{
    // Allocation-free 64-bit FNV-1a over the observed values, so the observer adds nothing to the measurement.
    internal sealed class Digest
    {
        private ulong value = 14695981039346656037UL;
        public long Count;
        public void Add(ulong item)
        {
            unchecked { for (int i = 0; i < 8; i++) { value ^= (byte)(item >> (i * 8)); value *= 1099511628211UL; } }
        }
        public void Add(double item) => Add((ulong)BitConverter.DoubleToInt64Bits(item));
        public void Add(int item) => Add((ulong)(uint)item);
        public void Add(string? item)
        {
            if (item == null) { Add(ulong.MaxValue); return; }
            Add(item.Length);
            foreach (char c in item) Add(c);
        }
        public string Hex => value.ToString("x16", CultureInfo.InvariantCulture);
    }

    // Grid roads at 50 m spacing with native tags, walking elsewhere, and a deterministic refusal.
    // The answer is a pure function of the request and the resident's car, as a native planner's would be.
    internal sealed class Planner : ICitizenRoutePlanner, ICommuteTimingPlanner, ITrafficRoutePlanner
    {
        public const int Size = 31;
        public const double Spacing = 50;
        public const string Native = "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00";
        public TrafficNetwork? TrafficNetwork { get; }
        public Planner()
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
                    nodes.Add(new TrafficNetworkNode { Id = Id(x, z), Position = Node(x, z), KilometresPerHour = 36, AllowsCar = true,
                        Boundary = true, StructuralContract = "road", Successors = successors.ToArray() });
                }
            // 36 km/h at one game minute per real minute: a 50 m section takes 1/12 minute, matching the car legs.
            TrafficNetwork = new TrafficNetwork(Native, nodes, 1.0 / 60);
        }
        public static int Id(int x, int z) => x * Size + z;
        public static Position Node(int x, int z) => new Position(x * Spacing, 0, z * Spacing);
        private static (int X, int Z) Nearest(Position p) => (Math.Max(0, Math.Min(Size - 1, (int)Math.Round(p.X / Spacing))),
            Math.Max(0, Math.Min(Size - 1, (int)Math.Round(p.Z / Spacing))));
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;

        // Allocation inside the planner, which is harness code, so the simulator's own share can be reported.
        public long Bytes, Requests, CarRoutes;
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences) => Measured(from, to, departureMinute, null, preferences);
        public Route? Plan(Place from, Place to, double departureMinute, CitizenState citizen) => Measured(from, to, departureMinute, citizen, citizen.Preferences);
        private Route? Measured(Place from, Place to, double minute, CitizenState? citizen, TravelPreferences preferences)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var route = Plan(from, to, minute, citizen, preferences);
            Bytes += GC.GetAllocatedBytesForCurrentThread() - before;
            Requests++;
            if (route != null && route.Legs.Any(l => l.Mode == TravelMode.Car)) CarRoutes++;
            return route;
        }

        private static Route? Plan(Place from, Place to, double minute, CitizenState? citizen, TravelPreferences preferences)
        {
            uint draw = StableRandom.Hash(from.Id + ">" + to.Id + "@" + minute.ToString("R", CultureInfo.InvariantCulture), 3);
            if (draw % 13 == 0) return null;
            var route = new Route();
            if (citizen != null && preferences.OwnsCar && Position.Distance(from.Position, to.Position) > 350)
            {
                if (citizen.Car == null && citizen.Phase == CitizenPhase.AtPlace && citizen.PlaceId == citizen.HomeId && from.Id == citizen.HomeId)
                {
                    var home = Nearest(from.Position);
                    citizen.Car = new CarState { Id = citizen.Identity.Id + ":car", ModelId = "test", ParkedPosition = Node(home.X, home.Z) };
                }
                if (citizen.Car != null && Position.Distance(from.Position, citizen.Car.ParkedPosition) < 250)
                {
                    var start = Nearest(citizen.Car.ParkedPosition);
                    var end = Nearest(to.Position);
                    if (Position.Distance(Node(start.X, start.Z), citizen.Car.ParkedPosition) < 1e-9 && start != end)
                    {
                        Walk(route, from.Position, citizen.Car.ParkedPosition);
                        int x = start.X, z = start.Z;
                        while (x != end.X || z != end.Z)
                        {
                            int nx = x != end.X ? x + Math.Sign(end.X - x) : x;
                            int nz = x != end.X ? z : z + Math.Sign(end.Z - z);
                            route.Legs.Add(new RouteLeg { From = Node(x, z), To = Node(nx, nz), Mode = TravelMode.Car, Minutes = Spacing / 600.0,
                                ServiceId = "native:road:v1:" + Native + ":" + Id(x, z).ToString(CultureInfo.InvariantCulture) + ":" + Id(nx, nz).ToString(CultureInfo.InvariantCulture) });
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

    internal static class Program
    {
        private static uint state = 12345;
        private static double Next() { state = state * 1664525u + 1013904223u; return (state >> 8) / 16777216.0; }
        private static int Pick(int n) => (int)(Next() * n);

        private static List<Place> Places()
        {
            var places = new List<Place>();
            double extent = (Planner.Size - 1) * Planner.Spacing;
            void Add(string prefix, PlaceKind kind, int count, int capacity)
            {
                for (int i = 0; i < count; i++)
                {
                    var p = new Place
                    {
                        Id = prefix + "-" + i.ToString("D4", CultureInfo.InvariantCulture), Name = prefix + " " + i, Kind = kind, Capacity = capacity,
                        Position = new Position(Next() * extent, 0, Next() * extent), AttractionWeight = 0.5 + Next() * 3,
                        NativeTrafficIndex = Pick(4) == 0 ? -1 : Pick(120), EvidenceSource = "synthetic"
                    };
                    p.District = p.Position.X < extent / 2 ? "West" : "East";
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
            Add("home", PlaceKind.Home, 500, 3);
            Add("work", PlaceKind.Work, 150, 12);
            Add("study", PlaceKind.Study, 20, 40);
            Add("shop", PlaceKind.Shop, 170, 30);
            Add("leisure", PlaceKind.Leisure, 171, 30);
            return places;
        }

        private static string Sha(string text)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        private static void Observe(Digest digest, string id, CitizenState c, double minute)
        {
            digest.Count++;
            digest.Add(id); digest.Add(minute); digest.Add((int)c.Phase); digest.Add((int)c.Activity); digest.Add(c.PlaceId);
            digest.Add(c.DestinationId); digest.Add((int)c.DestinationActivity); digest.Add(c.NextEventMinute); digest.Add(c.ActivityUntilMinute);
            digest.Add(c.DepartureMinute); digest.Add(c.Journey?.Legs.Count ?? -1); digest.Add(c.MissedActivities);
        }

        public static int Main(string[] args)
        {
            int residents = 600, days = 6;
            var places = Places();
            var identities = Enumerable.Range(0, residents).Select(i => new CitizenIdentity
            {
                Id = "resident:" + i.ToString("D4", CultureInfo.InvariantCulture), Name = "Resident " + i, Age = i % 8 == 4 ? 70 : i % 8 == 3 ? 20 : 35,
                Female = i % 2 == 0, District = i % 3 == 0 ? "West" : "East", Persona = (Persona)(i % 8), AppearanceSeed = i + 1
            }).ToList();
            var city = Population.Create(identities, places, "core-identity-regression", "synthetic", 20261028, 1440);
            var planner = new Planner();
            var events = new Digest();
            var simulator = new CitySimulator(city, planner);
            Action<string, CitizenState, double> observer = (id, c, minute) => Observe(events, id, c, minute);
            simulator.ActivityObserved += observer;
            var saves = new List<string>();
            var trafficAtSaves = new List<int>();
            long advanceBytes = 0, frames = 0, eventsRun = 0, trafficEvents = 0;
            double start = city.Minute;
            long frame = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            // A save at every midnight, and one during the morning commute of day 3 at the first frame with at
            // least three cars in traffic. That one is reloaded into a new simulator, so committed car journeys
            // rebind to the network from their saved strings and the transient caches are rebuilt from load.
            double searchFrom = start + 2 * 1440 + 420;
            bool reloaded = false;
            for (int day = 1; day <= days; day++)
            {
                double checkpoint = start + day * 1440.0;
                bool reload = false;
                while (true)
                {
                    double target = Math.Min(checkpoint, start + (frame + 1) * 0.25);
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    var result = simulator.AdvanceFrameSlice(target, 24);
                    advanceBytes += GC.GetAllocatedBytesForCurrentThread() - before;
                    frames++; eventsRun += result.Events; trafficEvents += result.TrafficEvents;
                    if (!simulator.IsCaughtUpTo(target)) continue;
                    frame++;
                    if (!reloaded && target >= searchFrom)
                    {
                        simulator.State.PrepareForSave();
                        if ((simulator.State.Traffic?.Vehicles.Count ?? 0) >= 3) { reload = true; break; }
                    }
                    if (target >= checkpoint) break;
                }
                string json = SaveCodec.Write(simulator.State);
                trafficAtSaves.Add(simulator.State.Traffic?.Vehicles.Count ?? -1);
                saves.Add(Sha(json));
                if (reload)
                {
                    reloaded = true; day--;
                    // Transient caches must be rebuilt from the loaded strings with the same results.
                    var loaded = SaveCodec.Read(json);
                    if (loaded.State == null) throw new InvalidOperationException("Reload failed: " + loaded.Error);
                    simulator.ActivityObserved -= observer;
                    simulator = new CitySimulator(loaded.State, planner);
                    simulator.ActivityObserved += observer;
                    if (Sha(SaveCodec.Write(simulator.State)) != saves[saves.Count - 1]) throw new InvalidOperationException("Reload changed the save.");
                }
            }
            double simulateMs = watch.Elapsed.TotalMilliseconds;
            var final = simulator.State;
            int carTrips = final.Citizens.Count(c => c.Car != null);

            // Helper-level probes over the final city, through public APIs present in both trees.
            var routine = new Digest();
            long before2 = GC.GetAllocatedBytesForCurrentThread();
            foreach (var person in final.Citizens)
                for (double minute = final.Minute; minute < final.Minute + 2880; minute += 37)
                {
                    var slot = DailyRoutine.At(person, final.Seed, minute, final.Places);
                    routine.Add(slot.PlaceId); routine.Add((int)slot.Activity); routine.Add(slot.EndsAt);
                    var work = DailyRoutine.NextWork(person, final.Seed, minute);
                    routine.Add(work.HasValue ? work.Value.StartsAt : -1);
                    routine.Add(DailyRhythm.OnDay(person, final.Seed, (int)(minute / 1440)).StartsAt);
                }
            long routineBytes = GC.GetAllocatedBytesForCurrentThread() - before2;

            var opening = new Digest();
            before2 = GC.GetAllocatedBytesForCurrentThread();
            foreach (var place in final.Places)
                for (double minute = final.Minute; minute < final.Minute + 2880; minute += 13)
                {
                    opening.Add(DailyRoutine.NextClose(place, minute) ?? -1);
                    opening.Add(DailyRoutine.NextOpen(place, minute, minute + 600) ?? -1);
                    opening.Add(DailyRoutine.Open(place, minute) ? 1 : 0);
                }
            long openingBytes = GC.GetAllocatedBytesForCurrentThread() - before2;

            var choice = new Digest();
            before2 = GC.GetAllocatedBytesForCurrentThread();
            foreach (var person in final.Citizens)
            {
                var home = final.Places.First(p => p.Id == person.HomeId);
                choice.Add(DestinationChoice.Choose(final.Places, home, PlaceKind.Leisure, person.Identity.Id + ":probe", final.Seed));
                foreach (var p in DestinationChoice.Shortlist(final.Places, home, PlaceKind.Shop, person.Identity.Id + ":list", final.Seed)) choice.Add(p.Id);
            }
            long choiceBytes = GC.GetAllocatedBytesForCurrentThread() - before2;

            // A 300-leg serpentine car route mapped at 100 progress points.
            var serpentine = new Route();
            int sx = 0, sz = 0;
            for (int i = 0; i < 300; i++)
            {
                int nx = sx, nz = sz;
                if (sx % 2 == 0 ? sz < Planner.Size - 1 : sz > 0) nz = sz + (sx % 2 == 0 ? 1 : -1); else nx = sx + 1;
                serpentine.Legs.Add(new RouteLeg { From = Planner.Node(sx, sz), To = Planner.Node(nx, nz), Mode = TravelMode.Car, Minutes = Planner.Spacing / 600.0,
                    ServiceId = "native:road:v1:" + Planner.Native + ":" + Planner.Id(sx, sz).ToString(CultureInfo.InvariantCulture) + ":" + Planner.Id(nx, nz).ToString(CultureInfo.InvariantCulture) });
                sx = nx; sz = nz;
            }
            var mapping = new Digest();
            before2 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                bool ok = planner.TrafficNetwork!.TryMapRemaining(serpentine, serpentine.Minutes * i / 100.0, out var mapped);
                mapping.Add(ok ? mapped.Count : -1);
                foreach (var leg in mapped) { mapping.Add(leg.LinkId); mapping.Add(leg.StartNominalMinutes); mapping.Add(leg.FreeFlowMinutes); mapping.Add(leg.StartLinkFraction); }
            }
            long mappingBytes = GC.GetAllocatedBytesForCurrentThread() - before2;

            var output = new StringBuilder();
            output.Append('{');
            output.Append("\"residents\":").Append(residents).Append(",\"places\":").Append(places.Count).Append(",\"days\":").Append(days);
            output.Append(",\"events\":").Append(events.Count).Append(",\"eventDigest\":\"").Append(events.Hex).Append('"');
            output.Append(",\"advanceEvents\":").Append(eventsRun).Append(",\"frames\":").Append(frames).Append(",\"residentsWithCars\":").Append(carTrips);
            output.Append(",\"trafficEvents\":").Append(trafficEvents);
            output.Append(",\"routeRequests\":").Append(planner.Requests).Append(",\"carRoutes\":").Append(planner.CarRoutes);
            output.Append(",\"trafficVehiclesAtSaves\":[").Append(string.Join(",", trafficAtSaves)).Append(']');
            output.Append(",\"saves\":[").Append(string.Join(",", saves.Select(s => "\"" + s + "\""))).Append(']');
            output.Append(",\"routineDigest\":\"").Append(routine.Hex).Append("\",\"openingDigest\":\"").Append(opening.Hex);
            output.Append("\",\"choiceDigest\":\"").Append(choice.Hex).Append("\",\"mappingDigest\":\"").Append(mapping.Hex).Append('"');
            output.Append(",\"bytes\":{\"advance\":").Append(advanceBytes).Append(",\"planner\":").Append(planner.Bytes).Append(",\"routine\":").Append(routineBytes)
                .Append(",\"opening\":").Append(openingBytes).Append(",\"choice\":").Append(choiceBytes).Append(",\"mapping\":").Append(mappingBytes).Append('}');
            output.Append(",\"simulateMs\":").Append(simulateMs.ToString("F0", CultureInfo.InvariantCulture));
            output.Append('}');
            Console.WriteLine(output.ToString());
            return 0;
        }
    }
}
