using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5AllocationMeasure
{
    private sealed class AllocationTypes : EventListener
    {
        public readonly Dictionary<string, long> Bytes = new Dictionary<string, long>();
        public bool Recording;

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (!Recording || e.EventName == null || !e.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal) || e.Payload == null) return;
            int name = e.PayloadNames!.IndexOf("TypeName"), amount = e.PayloadNames.IndexOf("AllocationAmount64");
            if (name < 0) return;
            string type = (string)e.Payload[name]!;
            long bytes = amount >= 0 ? Convert.ToInt64(e.Payload[amount]) : 100_000;
            lock (Bytes) Bytes[type] = (Bytes.TryGetValue(type, out long b) ? b : 0) + bytes;
        }
    }

    private sealed class StopSpots : IOptionalStopEnvironment
    {
        private readonly Dictionary<(string, DwellKind), OptionalStopSpot[]> spots = new Dictionary<(string, DwellKind), OptionalStopSpot[]>();
        public bool Raining => false;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
        {
            if (spots.TryGetValue((place.Id, kind), out var known)) return known;
            var made = Enumerable.Range(0, 4).Select(i => new OptionalStopSpot { Id = place.Id + ":" + kind + ":" + i, TargetId = place.Id,
                Position = new Position(place.Position.X + i, 0, place.Position.Z + 2 + (int)kind * 3), Facing = place.Position }).ToArray();
            spots.Add((place.Id, kind), made);
            return made;
        }
        public OptionalStopAvailability Available(OptionalStopState stop) => OptionalStopAvailability.Ready;
    }

    private static void Frame(CitySimulator simulator, double minute)
    {
        simulator.RefreshOptionalStops();
        simulator.AdvanceFrameSlice(minute, 32);
    }

    internal static CityState City()
    {
        string? source = PerfGate.Variable("PC_GR5_PAYLOAD");
        if (!string.IsNullOrEmpty(source))
        {
            var read = SaveCodec.Read(CapturedPayload.Load(source));
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            return read.State!;
        }
        var state = EconomyScenario.Create(CityBalance.CityResidentTotal, RepresentativeFixture.Seed);
        PortalPlaces.Add(state, RepresentativeFixture.Portals);
        VisitorSchedules.Create(state, 2 * CityBalance.CityResidentTotal);
        state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
            Shops = Enumerable.Range(1, 3).Select(i => new FoodScope { PlaceId = "shop-" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
        return state;
    }

    internal const int RigPlaces = 1683;

    internal static CityState RigCity()
    {
        const string Nightlife = "ba:businesstype_nightclub";
        int seed = RepresentativeFixture.Seed;
        Position At(string id) => new Position(StableRandom.Hash(id + ":x", seed) % 2400, 0, StableRandom.Hash(id + ":z", seed) % 2400);
        var places = new List<Place>();
        void Add(string id, PlaceKind kind, int capacity, string type = "", int open = 0, int close = 1440)
        {
            var at = At(id);
            places.Add(new Place { Id = id, Name = id, Kind = kind, Capacity = capacity, Position = at, BusinessType = type, OpenMinute = open, CloseMinute = close,
                District = (at.X < 1200 ? "a" : "b") + (at.Z < 1200 ? "n" : "s") });
        }
        for (int i = 0; i < 557; i++) Add("home-" + i, PlaceKind.Home, i % 4 == 3 ? 4 : 3);
        for (int i = 0; i < 577; i++) Add("work-" + i, PlaceKind.Work, 11, i % 29 == 0 ? Nightlife : "");
        Add("school", PlaceKind.Study, 20);
        for (int i = 1; i <= 350; i++) Add("shop-" + i, PlaceKind.Shop, 1, "", 480, 1260);
        for (int i = 0; i < 178; i++) Add("leisure-" + i, PlaceKind.Leisure, 1, i % 12 == 0 ? Nightlife : "", i % 12 == 0 ? 0 : 600, i % 12 == 0 ? 1440 : 1380);
        var identities = Enumerable.Range(0, CityBalance.CityResidentTotal).Select(i => new CitizenIdentity
        {
            Id = "rlo:rig:c" + i.ToString("D5"), Name = "Citizen " + i, Age = 20 + i % 50,
            District = (i % 4 < 2 ? "a" : "b") + (i % 2 == 0 ? "n" : "s"), Persona = (Persona)(i % 8), AppearanceSeed = i + 1,
        });
        var state = Population.Create(identities, places, "gr5-rig-scale", "0.41.1", seed, 1440 + 300);
        PortalPlaces.Add(state, Enumerable.Range(0, 20).Select(i => new PortalStation("subway:rig-" + i.ToString("D2"), At("subway-" + i))));
        VisitorSchedules.Create(state, 2 * CityBalance.CityResidentTotal);
        state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
            Shops = Enumerable.Range(1, 3).Select(i => new FoodScope { PlaceId = "shop-" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
        Assert.Equal(RigPlaces, state.Places.Count);
        Assert.Equal(CityBalance.CityResidentTotal, state.Citizens.Count);
        Assert.Equal(2 * CityBalance.CityResidentTotal, state.Visitors.Count);
        return state;
    }

    [Fact]
    public void MeasureDayAndNightHourAllocation()
    {
        string? path = PerfGate.Variable("PC_GR5_ALLOC");
        if (string.IsNullOrEmpty(path)) return;
        string label = PerfGate.Variable("PC_GR5_ALLOC_LABEL") ?? "";
        const int fps = 30;
        using var types = new AllocationTypes();
        for (int run = 1; run <= 3; run++)
        {
            var state = City();
            var simulator = new CitySimulator(state, new FreshCityTraceTests.Walk());
            simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            simulator.AttachOptionalStops(new StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
            double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
            foreach (var (window, start) in new[] { ("night-01:00", day + 60), ("day-15:30", day + 15.5 * 60) })
            {
                simulator.AdvanceTo(start - 30, int.MaxValue);
                for (double minute = start - 30; minute < start; minute += 1.0 / fps) Frame(simulator, minute);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                lock (types.Bytes) types.Bytes.Clear();
                types.Recording = run == 3;
                int gen0 = GC.CollectionCount(0), frames = 0;
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (double minute = start; minute < start + 60; minute += 1.0 / fps) { Frame(simulator, minute); frames++; }
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                types.Recording = false;
                List<KeyValuePair<string, long>> top;
                lock (types.Bytes) top = types.Bytes.OrderByDescending(p => p.Value).Take(15).ToList();
                System.IO.File.AppendAllText(path, JsonConvert.SerializeObject(new
                {
                    label, run, window, optionalStops = OptionalStopPolicy.ApprovedChoicePercentage, residents = state.Citizens.Count, visitors = state.Visitors.Count, places = state.Places.Count,
                    gameMinutes = 60, frames, allocatedBytes = allocated, bytesPerGameMinute = allocated / 60, bytesPerFrame = allocated / frames,
                    gen0 = GC.CollectionCount(0) - gen0,
                    topTypes = run == 3 ? top.ToDictionary(p => p.Key, p => p.Value) : null,
                }) + Environment.NewLine);
            }
        }
    }

    private sealed class Walk : IRoutePlanner
    {
        public Route Plan(Place a, Place b, double at, TravelPreferences p) => Legs(a.Position, b.Position);
    }

    private static Route Legs(Position a, Position b)
    {
        var route = new Route { Legs = new() };
        for (int i = 0; i < 6; i++)
        {
            var from = Position.Lerp(a, b, i / 6.0); var to = Position.Lerp(a, b, (i + 1) / 6.0);
            route.Legs.Add(new RouteLeg { From = from, To = to, Mode = TravelMode.Walk, Minutes = Position.Distance(from, to) / 2 });
        }
        return route;
    }

    private static void Record(string path, object row) => System.IO.File.AppendAllText(path, JsonConvert.SerializeObject(row) + Environment.NewLine);

    private static Dictionary<string, long> Top(AllocationTypes types)
    {
        lock (types.Bytes) return types.Bytes.OrderByDescending(p => p.Value).Take(15).ToDictionary(p => p.Key, p => p.Value);
    }

    [Fact]
    public void MeasurePhysicalPopulationUpdateAllocation()
    {
        string? path = PerfGate.Variable("PC_GR5_ALLOC");
        if (string.IsNullOrEmpty(path)) return;
        string label = PerfGate.Variable("PC_GR5_ALLOC_LABEL") ?? "";
        const int walkers = 48, frames = 7200, cycle = 240;
        const double now = 1440 + 600;
        const float frame = 1 / 60f;
        using var types = new AllocationTypes();
        for (int run = 1; run <= 3; run++)
        {
            StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrianBody.Unreachable = null;
            PhysicalPedestrians.VisiblePoint = null; PhysicalPedestrians.AppearOnScreen = false;
            var ids = Enumerable.Range(0, walkers).Select(i => new CitizenIdentity { Id = "w" + i.ToString("D2"), Name = "W" + i, Age = 30, AppearanceSeed = i + 1 }).ToList();
            var places = new List<Place>
            {
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = walkers, Position = new Position(100, 0, 0) },
                new Place { Id = "venue", Name = "Venue", Kind = PlaceKind.Leisure, Capacity = walkers, Position = new Position(100, 0, 20) },
            };
            for (int i = 0; i < walkers; i++)
                places.Add(new Place { Id = "home" + i, Name = "Home", Kind = PlaceKind.Home, Capacity = 1,
                    Position = new Position(100 + 90 * Math.Cos(i * 0.13), 0, 90 * Math.Sin(i * 0.13)) });
            var state = Population.Create(ids, places, "gr5-alloc", "0.40.4", 3, now);
            var stops = new List<OptionalStopState[]>();
            for (int i = 0; i < walkers; i++)
            {
                var c = state.Citizens[i];
                c.HomeId = "home" + i; c.DayPlaceId = "work"; c.MealOuting = null;
                if (i % 2 == 0)
                {
                    c.PlaceId = "home" + i; c.Phase = CitizenPhase.Travelling; c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
                    c.Journey = Legs(places[i + 2].Position, places[0].Position);
                    c.DepartureMinute = now; c.NextEventMinute = now + c.Journey.Minutes; c.ActivityUntilMinute = now + 600;
                    continue;
                }
                c.PlaceId = "venue"; c.Activity = Activity.Leisure; c.Phase = CitizenPhase.AtPlace; c.Journey = null;
                c.NextEventMinute = c.ActivityUntilMinute = now + 600;
                var spot = new Position(100 + (i % 12) * 1.5, 0, 22 + i / 12 * 1.5);
                var own = new OptionalStopState[frames / cycle + 2];
                for (int k = 0; k < own.Length; k++)
                    own[k] = new OptionalStopState { Kind = DwellKind.WindowShopping, PlaceId = "venue", SpotId = "solo:" + ids[i].Id + ":" + k, TargetId = "front:" + ids[i].Id,
                        Position = spot, Facing = new Position(spot.X, 0, spot.Z + 1), ReturnPosition = places[1].Position,
                        StartMinute = now + k * cycle * frame / 3.0, EndMinute = now + (k + 0.5) * cycle * frame / 3.0,
                        VisitMinute = now + k * cycle * frame / 3.0, ResumeEventMinute = now + 600 };
                c.OptionalStop = own[0];
                stops.Add(own);
            }
            var city = new CitySimulator(state, new Walk());
            NativeTravelPace.Focus = new Position(100, 0, 0); Time.Next(3);
            using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
                typeof(Gr5AllocationMeasure).Assembly, new VendorBodyTests.Log(), 64, 8, 120, new DwellSpots());
            var pool = PhysicalPedestrians.Last!;
            double minute = now;
            var split = run == 2 ? new GcPauseSampler(64) : null;
            split?.Start(GC.GetTotalMemory(false), GcAllocationCounter.Probe());
            long allocated = 0;
            int gen0 = 0;
            void Move(float seconds)
            {
                foreach (var body in pool.Active.Values)
                {
                    if (body.Agent.isStopped) continue;
                    var at = body.Position; double d = Position.Distance(at, body.Target), step = Math.Min(body.Speed > 0 ? body.Speed : 1.4, 1.4) * seconds;
                    body.Character.transform.position = PhysicalPedestrianBody.Vector(d <= step ? body.Target : Position.Lerp(at, body.Target, step / d));
                }
            }
            void Frame(int index, bool measure)
            {
                if (index % cycle == 0)
                {
                    int k = index / cycle, j = 0;
                    for (int i = 1; i < walkers; i += 2) state.Citizens[i].OptionalStop = stops[j++][k];
                }
                Move(frame); minute += frame / 3.0; city.AdvanceTo(minute); Time.Next(frame);
                if (!measure) { bodies.Update(); return; }
                int before = GC.CollectionCount(0);
                types.Recording = run == 3;
                long start = GC.GetAllocatedBytesForCurrentThread();
                PhysicalAllocation.Begin(split);
                bodies.Update();
                PhysicalAllocation.End();
                allocated += GC.GetAllocatedBytesForCurrentThread() - start;
                types.Recording = false;
                gen0 += GC.CollectionCount(0) - before;
            }
            for (int i = 0; i < cycle * 2; i++) Frame(i, false);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            lock (types.Bytes) types.Bytes.Clear();
            for (int i = cycle * 2; i < frames; i++) Frame(i, true);
            int measured = frames - cycle * 2;
            Record(path, new
            {
                label, run, component = "PhysicalPopulation.Update", walkers, optionalWalkers = walkers / 2, frames = measured,
                boundBodies = pool.Active.Count,
                allocatedBytes = allocated, bytesPerFrame = allocated / measured, gen0,
                split = split == null ? null : Enumerable.Range((int)GcAllocationSegment.Physical, GcPauseSampler.Segments - (int)GcAllocationSegment.Physical)
                    .Select(i => (GcAllocationSegment)i).ToDictionary(segment => segment.ToString(), segment => split.SegmentBytes(segment)),
                counter = split?.AllocationCounter,
                topTypes = run == 3 ? Top(types) : null,
            });
        }
    }

    private sealed class RoadGrid : ICommuteTimingPlanner
    {
        private const double Spacing = 40;
        private readonly TransportRoadGraph graph;
        private readonly double minX, minZ;
        private readonly int columns, rows;
        public long Searches;

        public RoadGrid(IReadOnlyList<Place> places)
        {
            minX = places.Min(p => p.Position.X) - Spacing; minZ = places.Min(p => p.Position.Z) - Spacing;
            columns = (int)Math.Ceiling((places.Max(p => p.Position.X) + Spacing - minX) / Spacing) + 1;
            rows = (int)Math.Ceiling((places.Max(p => p.Position.Z) + Spacing - minZ) / Spacing) + 1;
            var nodes = new TransportRoadNode[columns * rows];
            for (int x = 0; x < columns; x++)
                for (int z = 0; z < rows; z++)
                {
                    var next = new List<int>(4);
                    if (x > 0) next.Add(Index(x - 1, z)); if (x + 1 < columns) next.Add(Index(x + 1, z));
                    if (z > 0) next.Add(Index(x, z - 1)); if (z + 1 < rows) next.Add(Index(x, z + 1));
                    nodes[Index(x, z)] = new TransportRoadNode { Position = new Position(minX + x * Spacing, 0, minZ + z * Spacing), Successors = next.ToArray(),
                        KilometresPerHour = 30 + (x * 7 + z * 3) % 3 * 10, AllowsCar = true };
                }
            graph = new TransportRoadGraph(nodes);
            Nodes = nodes.Length;
        }

        public int Nodes { get; }
        private int Index(int x, int z) => x * rows + z;
        private int Nearest(Position p) => Index(Math.Clamp((int)Math.Round((p.X - minX) / Spacing), 0, columns - 1), Math.Clamp((int)Math.Round((p.Z - minZ) / Spacing), 0, rows - 1));

        public long SearchBytes, CopyBytes;

        public Route Plan(Place a, Place b, double at, TravelPreferences p)
        {
            int start = Nearest(a.Position), goal = Nearest(b.Position);
            Route? road = null;
            long began = GC.GetAllocatedBytesForCurrentThread();
            if (start != goal)
            {
                Searches++;
                road = graph.PlanWithTraversal(start, goal, _ => false, (_, _) => true, (_, _, baseline) => baseline * 1.1, 30000, null, (_, _) => 0.05, null);
            }
            long searched = GC.GetAllocatedBytesForCurrentThread();
            SearchBytes += searched - began;
            try { return Copy(a, b, road); }
            finally { CopyBytes += GC.GetAllocatedBytesForCurrentThread() - searched; }
        }

        private static Route Copy(Place a, Place b, Route? road)
        {
            if (road == null) return Legs(a.Position, b.Position);
            var route = new Route { Legs = new() };
            Position previous = a.Position;
            foreach (var leg in road.Legs) { route.Legs.Add(new RouteLeg { From = previous, To = leg.To, Mode = TravelMode.Walk, Minutes = Math.Max(0.01, leg.Minutes) }); previous = leg.To; }
            route.Legs.Add(new RouteLeg { From = previous, To = b.Position, Mode = TravelMode.Walk, Minutes = Math.Max(0.01, Position.Distance(previous, b.Position) / 80) });
            return route;
        }
    }

    [Fact]
    public void MeasureRigScaleAdvanceAllocation()
    {
        string? path = PerfGate.Variable("PC_GR5_ALLOC");
        if (string.IsNullOrEmpty(path)) return;
        string label = PerfGate.Variable("PC_GR5_ALLOC_LABEL") ?? "";
        const int fps = 90;
        using var types = new AllocationTypes();
        for (int run = 1; run <= 3; run++)
        {
            var state = RigCity();
            var grid = new RoadGrid(state.Places);
            var simulator = new CitySimulator(state, grid);
            simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            simulator.AttachOptionalStops(new StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
            double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
            foreach (var (window, start, minutes) in new[] { ("night-01:00", day + 60, 190), ("day-15:30", day + 15.5 * 60, 174) })
            {
                simulator.AdvanceTo(start - 30, int.MaxValue);
                for (double minute = start - 30; minute < start; minute += 1.0 / fps) Frame(simulator, minute);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                lock (types.Bytes) types.Bytes.Clear();
                types.Recording = run == 3;
                long searches = grid.Searches;
                int gen0 = GC.CollectionCount(0), frames = 0;
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (double minute = start; minute < start + minutes; minute += 1.0 / fps) { Frame(simulator, minute); frames++; }
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                types.Recording = false;
                Record(path, new
                {
                    label, run, component = "CitySimulator.AdvanceFrameSlice+RoadGrid", window, residents = state.Citizens.Count, visitors = state.Visitors.Count,
                    places = state.Places.Count, roadNodes = grid.Nodes, roadSearches = grid.Searches - searches,
                    gameMinutes = minutes, frames, allocatedBytes = allocated, bytesPerGameMinute = allocated / minutes, bytesPerFrame = allocated / frames,
                    gen0 = GC.CollectionCount(0) - gen0,
                    topTypes = run == 3 ? Top(types) : null,
                });
                Assert.True(grid.Searches - searches > 0, window + " made no road searches.");
            }
        }
    }

    [Fact]
    public void MeasureRetainedHeapAtTheCap()
    {
        string? path = PerfGate.Variable("PC_GR5_ALLOC");
        if (string.IsNullOrEmpty(path)) return;
        string label = PerfGate.Variable("PC_GR5_ALLOC_LABEL") ?? "";
        for (int run = 1; run <= 3; run++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long empty = GC.GetTotalMemory(true);
            Record(path, RetainedOnce(label, run, empty));
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object RetainedOnce(string label, int run, long empty)
    {
        var state = RigCity();
        long built = GC.GetTotalMemory(true);
        var grid = new RoadGrid(state.Places);
        var simulator = new CitySimulator(state, grid);
        simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        simulator.AttachOptionalStops(new StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
        double day = Math.Floor(state.Minute / 1440) * 1440;
        var rows = new List<object>();
        foreach (var (window, minute) in new[] { ("day-15:30", day + 15.5 * 60), ("night-01:00", day + 1440 + 60) })
        {
            simulator.AdvanceTo(minute, int.MaxValue);
            for (double m = minute; m < minute + 5; m += 1.0 / 90) Frame(simulator, m);
            long retained = GC.GetTotalMemory(true);
            int journeys = 0, legs = 0;
            foreach (var c in state.Citizens) if (c.Journey != null) { journeys++; legs += c.Journey.Legs.Count; }
            foreach (var v in state.Visitors) if (v.Routine?.Journey != null) { journeys++; legs += v.Routine.Journey.Legs.Count; }
            rows.Add(new { window, retainedBytes = retained - empty, perPersonBytes = (retained - empty) / (state.Citizens.Count + state.Visitors.Count), journeys, legs });
        }
        GC.KeepAlive(simulator); GC.KeepAlive(grid);
        return new { label, run, component = "CityState+CitySimulator retained (GC.GetTotalMemory(true))", residents = state.Citizens.Count,
            visitors = state.Visitors.Count, places = state.Places.Count, stateBytes = built - empty, windows = rows };
    }

    private delegate void TimedReturn(StandInBody item, PersonDressing dressing, double seconds);
    private delegate bool Trim(double seconds, double idleSeconds, out StandInBody item);

    private sealed class BodyPart
    {
        public BodyPart? Next;
        public string Name = "";
        public float[] Data = Array.Empty<float>();
    }

    private sealed class StandInBody
    {
        public const int Parts = 400, FloatsPerPart = 48;
        public readonly BodyPart[] Parts_ = new BodyPart[Parts];
        public StandInBody(int seed)
        {
            BodyPart? previous = null;
            for (int i = 0; i < Parts; i++) previous = Parts_[i] = new BodyPart { Next = previous, Name = "part" + (seed + i), Data = new float[FloatsPerPart] };
        }
    }

    private sealed class BakeCaches
    {
        public readonly Dictionary<object, string[]> Names = new Dictionary<object, string[]>();
        public readonly Dictionary<int, float[][]> Scratch = new Dictionary<int, float[][]>();
        public readonly List<(float, float, float)> Vertices = new List<(float, float, float)>(), Normals = new List<(float, float, float)>();
        public readonly List<(float, float, float, float)> Tangents = new List<(float, float, float, float)>();
        public readonly BlendshapeBakePlan Plan = new BlendshapeBakePlan();

        public BakeCaches(int meshes, int shapesPerMesh, int mergedVertices)
        {
            for (int mesh = 0; mesh < meshes; mesh++)
            {
                var shapes = new string[shapesPerMesh];
                for (int j = 0; j < shapes.Length; j++) shapes[j] = "blendshape_" + j;
                Names.Add(new object(), shapes);
                int length = 500 + mesh * 20;
                if (!Scratch.ContainsKey(length)) Scratch.Add(length, new[] { new float[3 * length], new float[3 * length], new float[3 * length] });
            }
            for (int i = 0; i < mergedVertices; i++) { Vertices.Add(default); Normals.Add(default); Tangents.Add(default); }
            Vertices.Clear(); Normals.Clear(); Tangents.Clear();
            int vertices = 0;
            for (int source = 0; source < 9; source++)
            {
                Plan.AddSource(mergedVertices / 9);
                vertices += mergedVertices / 9;
                for (int shape = 0; shape < shapesPerMesh; shape++) Plan.AddShape("blendshape_" + shape, shape % 3 == 0 ? 0 : 25 + shape);
            }
            Plan.Build(vertices, 1);
        }
    }

    [Fact]
    public void MeasureRetainedPoolAndBakeAtTheCap()
    {
        string? path = PerfGate.Variable("PC_GR5_ALLOC");
        if (string.IsNullOrEmpty(path)) return;
        string label = PerfGate.Variable("PC_GR5_ALLOC_LABEL") ?? "";
        for (int run = 1; run <= 3; run++) Record(path, PoolOnce(label, run));
    }

    private static long Retained()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        return GC.GetTotalMemory(true);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object PoolOnce(string label, int run)
    {
        const int maximum = 96, persons = 3000, wardrobeMeshes = 150, shapesPerMesh = 12, mergedVertices = 12000;
        const double step = 1.0 / 30, day = 900, seconds = 1800;
        var pool = new DressedBodyPool<StandInBody>();
        var timedMethod = typeof(DressedBodyPool<StandInBody>).GetMethod("Return", new[] { typeof(StandInBody), typeof(PersonDressing), typeof(double) });
        var trimMethod = typeof(DressedBodyPool<StandInBody>).GetMethod("TryTrim");
        var timedReturn = timedMethod == null ? null : (TimedReturn)Delegate.CreateDelegate(typeof(TimedReturn), pool, timedMethod);
        var trim = trimMethod == null ? null : (Trim)Delegate.CreateDelegate(typeof(Trim), pool, trimMethod);
        long baseline = Retained();
        long bodyBytes;
        {
            long empty = Retained();
            var one = new StandInBody(-1);
            bodyBytes = Retained() - empty;
            GC.KeepAlive(one);
        }
        baseline = Retained();
        var bake = new BakeCaches(wardrobeMeshes, shapesPerMesh, mergedVertices);
        long bakeBytes = Retained() - baseline;
        var dice = new Random(0x6E5 + run);
        var dressings = new PersonDressing[persons];
        var until = new double[persons];
        var phase = new int[persons];
        var bodies = new StandInBody?[persons];
        for (int i = 0; i < persons; i++)
        {
            dressings[i] = new PersonDressing(new CitizenIdentity { Id = "p" + i, Name = "N" + i, Age = 20 + i % 60, Female = i % 2 == 0, AppearanceSeed = 1 + i * 7 }, 60);
            until[i] = dice.NextDouble() * 4000;
        }
        long personBytes = Retained() - baseline - bakeBytes;
        int owned = 0, active = 0, frame = 0, dayOwned = 0, dayActive = 0, dayIdle = 0;
        long dayBytes = 0;
        double nextTrim = 0, selectAt = 0;
        for (double now = 0; now < seconds; now += step, frame++)
        {
            bool dark = now >= day;
            for (int i = 0; i < persons; i++)
            {
                if (now < until[i]) continue;
                if (phase[i] == 1)
                {
                    if (bodies[i] != null)
                    {
                        if (timedReturn != null) timedReturn(bodies[i]!, dressings[i], now); else pool.Return(bodies[i]!, dressings[i]);
                        bodies[i] = null; active--;
                    }
                    phase[i] = 0; until[i] = dark && dice.NextDouble() < 0.95 ? double.PositiveInfinity : now + 60 + dice.NextDouble() * 900;
                }
                else if (dark && dice.NextDouble() < 0.95) until[i] = double.PositiveInfinity;
                else { phase[i] = 1; until[i] = now + 20 + dice.NextDouble() * 70; }
            }
            if (trim != null && now >= nextTrim && trim(now, 120, out _)) { owned--; nextTrim = now + 5; }
            if (now >= selectAt)
            {
                selectAt = now + 0.25;
                int attempts = 0;
                for (int i = 0; i < persons && active < maximum && attempts < 4; i++)
                {
                    if (phase[i] != 1 || bodies[i] != null) continue;
                    attempts++;
                    var choice = pool.Choose(dressings[i], owned, maximum, null, frame, now, out var body);
                    if (choice == BodyChoice.Full) continue;
                    if (choice == BodyChoice.Grow) { body = new StandInBody(i); owned++; }
                    bodies[i] = body; active++;
                }
            }
            if (Math.Abs(now - (day - step)) < step / 2) { dayOwned = owned; dayActive = active; dayIdle = pool.Count; dayBytes = Retained() - baseline; }
        }
        long nightBytes = Retained() - baseline;
        GC.KeepAlive(pool); GC.KeepAlive(bodies); GC.KeepAlive(bake); GC.KeepAlive(dressings);
        return new { label, run, component = "pool + bake caches + per-person dressing, rig-shaped churn, 96 maximum, 3,000 persons, day 900 s then night 900 s",
            trims = trim != null, standInBodyBytes = bodyBytes, bakeCacheBytes = bakeBytes, personDressingBytes = personBytes,
            dayActive, dayOwned, dayIdle, dayRetainedBytes = dayBytes, nightActive = active, nightOwned = owned, nightIdle = pool.Count, nightRetainedBytes = nightBytes };
    }

    [Fact]
    public void ASteadyStateAdvanceTickAtTheCapAllocatesNothing()
    {
        var state = RigCity();
        var grid = new RoadGrid(state.Places);
        var simulator = new CitySimulator(state, grid);
        simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        simulator.AttachOptionalStops(new StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
        double start = Math.Floor(state.Minute / 1440) * 1440 + 15.5 * 60;
        simulator.AdvanceTo(start - 20, int.MaxValue);
        const int fps = 90;
        for (double minute = start - 20; minute < start; minute += 1.0 / fps) Frame(simulator, minute);
        int idle = 0, busy = 0, events = 0;
        long idleBytes = 0, busyBytes = 0;
        using var types = new AllocationTypes();
        types.Recording = PerfGate.Variable("PC_GR5_TYPES") != null;
        long searchesBefore = grid.Searches, searchBytes = grid.SearchBytes, copyBytes = grid.CopyBytes;
        for (double minute = start; minute < start + 20; minute += 1.0 / fps)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            simulator.RefreshOptionalStops();
            var result = simulator.AdvanceFrameSlice(minute, 32);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            if (result.Events == 0 && !result.PlanningPending) { idle++; idleBytes += bytes; }
            else { busy++; busyBytes += bytes; events += result.Events; }
        }
        string? path = PerfGate.Variable("PC_GR5_ALLOC");
        if (!string.IsNullOrEmpty(path))
            Record(path, new { label = PerfGate.Variable("PC_GR5_ALLOC_LABEL") ?? "", component = "advance ticks 15:10-15:30", idleTicks = idle, idleBytes,
                busyTicks = busy, events, busyBytes, bytesPerEvent = events == 0 ? 0 : busyBytes / events, roadSearches = grid.Searches - searchesBefore,
                searchBytes = grid.SearchBytes - searchBytes, fixtureCopyBytes = grid.CopyBytes - copyBytes,
                simulatorBytesPerEvent = events == 0 ? 0 : (busyBytes - (grid.SearchBytes - searchBytes) - (grid.CopyBytes - copyBytes)) / events, topTypes = types.Recording ? Top(types) : null });
        Assert.Equal(CityBalance.CityResidentTotal, state.Citizens.Count);
        Assert.True(idle >= 200, "only " + idle + " idle ticks");
        Assert.True(busy > 0);
        Assert.Equal(0, idleBytes);
        Assert.True(busyBytes / events < 9303, busyBytes / events + " B per busy event");
    }

    [Fact]
    public void PooledEventShellsKeepTheRigCityAndCutTheBytesPerEvent()
    {
        (string Save, long Bytes, int Events) Run(bool pooled)
        {
            var state = RigCity();
            var grid = new RoadGrid(state.Places);
            var simulator = new CitySimulator(state, grid) { PoolEventShells = pooled };
            simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            simulator.AttachOptionalStops(new StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
            double start = Math.Floor(state.Minute / 1440) * 1440 + 15.5 * 60;
            simulator.AdvanceTo(start - 20, int.MaxValue);
            const int fps = 90;
            long bytes = 0; int events = 0;
            for (double minute = start - 20; minute < start + 20; minute += 1.0 / fps)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                long fixture = grid.SearchBytes + grid.CopyBytes;
                simulator.RefreshOptionalStops();
                var result = simulator.AdvanceFrameSlice(minute, 32);
                if (minute < start) continue;
                bytes += GC.GetAllocatedBytesForCurrentThread() - before - (grid.SearchBytes + grid.CopyBytes - fixture);
                events += result.Events;
            }
            simulator.SynchronizeSnapshot();
            return (SaveCodec.Write(simulator.State), bytes, events);
        }
        var fresh = Run(false);
        var pooled = Run(true);
        Assert.Equal(fresh.Save, pooled.Save);
        Assert.Equal(fresh.Events, pooled.Events);
        Assert.True(fresh.Events > 100, fresh.Events.ToString());
        long before = fresh.Bytes / fresh.Events, after = pooled.Bytes / pooled.Events;
        string? path = PerfGate.Variable("PC_GR5_ALLOC");
        if (!string.IsNullOrEmpty(path))
            Record(path, new { label = PerfGate.Variable("PC_GR5_ALLOC_LABEL") ?? "", component = "simulator bytes per advance event 15:10-15:30 (fixture planner excluded)",
                events = pooled.Events, freshCloneBytesPerEvent = before, pooledShellBytesPerEvent = after });
        Assert.True(after < before, after + " vs " + before);
    }

    [Fact]
    public void MeasureNativeRoadSpeedsRefreshAllocation()
    {
        string? path = PerfGate.Variable("PC_GR5_ALLOC");
        if (string.IsNullOrEmpty(path)) return;
        string label = PerfGate.Variable("PC_GR5_ALLOC_LABEL") ?? "";
        const int lanes = 2000, refreshes = 1800;
        using var types = new AllocationTypes();
        var snapshot = new Dictionary<object, (int Original, int Effective)>(NativeRoadSpeeds.ReferenceComparer);
        for (int i = 0; i < lanes; i++) snapshot.Add(new GleyTrafficSystem.Waypoint { maxSpeed = 30 + i % 3 * 10 }, (50, 30 + i % 3 * 10));
        for (int run = 1; run <= 3; run++)
        {
            var speeds = new NativeRoadSpeeds(() => snapshot);
            for (int i = 0; i < 30; i++) speeds.Refresh();
            Assert.True(speeds.Verified);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            lock (types.Bytes) types.Bytes.Clear();
            int gen0 = GC.CollectionCount(0);
            types.Recording = run == 3;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < refreshes; i++) speeds.Refresh();
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            types.Recording = false;
            Record(path, new
            {
                label, run, component = "NativeRoadSpeeds.Refresh", lanes, refreshes,
                allocatedBytes = allocated, bytesPerRefresh = allocated / refreshes, gen0 = GC.CollectionCount(0) - gen0,
                topTypes = run == 3 ? Top(types) : null,
            });
        }
    }

    [Fact]
    public void MeasureRigScaleDayRetainedGrowth()
    {
        if (PerfGate.Variable("PC_GR5_DAY_RETAINED") == null) return;
        var state = RigCity();
        var grid = new RoadGrid(state.Places);
        var simulator = new CitySimulator(state, grid);
        simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        simulator.AttachOptionalStops(new StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
        double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
        string? startText = PerfGate.Variable("PC_GR5_DAY_RETAINED_START");
        double start = day + (startText == null ? 15.5 * 60 : double.Parse(startText) * 60);
        simulator.AdvanceTo(start, int.MaxValue);
        Dictionary<string, (long, long)> Take(string title)
        {
            var heap = new RetainedHeap();
            heap.Add("simulator", simulator);
            var map = heap.Types.ToDictionary(r => r.Name, r => (r.Objects, r.Bytes));
            Console.WriteLine(title + " objects " + heap.Objects + " bytes " + heap.Bytes);
            foreach (var row in heap.Roots) Console.WriteLine("  root " + row.Name + " " + row.Bytes);
            return map;
        }
        var before = Take("start");
        double minute = start;
        foreach (double minutes in new[] { 60.0, 120, 174, 260 })
        {
            for (; minute < start + minutes; minute += 1.0 / 30) Frame(simulator, minute);
            var after = Take("+" + minutes);
            foreach (var row in after.Select(p => (p.Key, p.Value.Item1 - (before.TryGetValue(p.Key, out var b) ? b.Item1 : 0), p.Value.Item2 - (before.TryGetValue(p.Key, out var c) ? c.Item2 : 0)))
                .Where(r => Math.Abs(r.Item3) > 20000).OrderByDescending(r => Math.Abs(r.Item3)).Take(25))
                Console.WriteLine("   " + row.Key + " objects " + row.Item2 + " bytes " + row.Item3);
        }
    }
}
