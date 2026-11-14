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
                bodies.Update();
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
                topTypes = run == 3 ? Top(types) : null,
            });
        }
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
}
