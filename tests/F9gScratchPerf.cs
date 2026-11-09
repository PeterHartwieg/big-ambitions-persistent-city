// Scratch perf harness for F9g; not committed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class F9gScratchPerf
{
    private static string Stats(string label, List<double> times, long bytes, int seen)
    {
        int n = times.Count; times.Sort();
        return string.Format(CultureInfo.InvariantCulture, "{0}: bodies={1} n={2} p50={3:F4} ms p95={4:F4} ms max={5:F4} ms spikes>=30ms={6} alloc/update={7:F1} B",
            label, seen, n, times[n / 2], times[(int)Math.Ceiling(n * .95) - 1], times[^1], times.Count(t => t >= 30), bytes / (double)n);
    }

    [Fact]
    public void Measure()
    {
        string? path = System.Environment.GetEnvironmentVariable("PC_F9G_SCRATCH");
        if (path == null) return;
        var lines = new List<string>();
        for (int run = 0; run < 3; run++) { lines.Add(Walkers()); lines.Add(Cars()); }
        File.AppendAllText(path, string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine);
    }

    private static string Walkers()
    {
        var wt = typeof(PhysicalWalkerPerfTests);
        var walk = (IRoutePlanner)Activator.CreateInstance(wt.GetNestedType("Walk", BindingFlags.NonPublic)!, true)!;
        var legs = wt.GetMethod("Legs", BindingFlags.NonPublic | BindingFlags.Static)!;
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrians.VisiblePoint = null;
        const double now = 1440 + 600;
        var ids = Enumerable.Range(0, 48).Select(i => new CitizenIdentity { Id = "w" + i.ToString("D2"), Name = "W" + i, Age = 30, AppearanceSeed = i + 1 }).ToList();
        var places = new List<Place> { new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 64, Position = new Position(100, 0, 0) } };
        for (int i = 0; i < 48; i++)
            places.Add(new Place { Id = "home" + i, Name = "Home", Kind = PlaceKind.Home, Capacity = 1,
                Position = new Position(100 + 90 * Math.Cos(i * 0.13), 0, 90 * Math.Sin(i * 0.13)) });
        var state = Population.Create(ids, places, "f2c-perf", "0.40.4", 3, now);
        for (int i = 0; i < 48; i++)
        {
            var c = state.Citizens[i];
            c.HomeId = c.PlaceId = "home" + i; c.DayPlaceId = "work"; c.MealOuting = null;
            c.Phase = CitizenPhase.Travelling; c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
            c.Journey = (Route)legs.Invoke(null, new object[] { places[i + 1].Position, places[0].Position })!;
            c.DepartureMinute = now; c.NextEventMinute = now + c.Journey.Minutes; c.ActivityUntilMinute = now + 600;
        }
        var city = new CitySimulator(state, walk);
        NativeTravelPace.Focus = new Position(100, 0, 0); Time.Next(3);
        using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
            typeof(F9gScratchPerf).Assembly, new VendorBodyTests.Log(), 64, 8, 120, new DwellSpots());
        var pool = PhysicalPedestrians.Last!;
        void Move(float seconds)
        {
            foreach (var body in pool.Active.Values)
            {
                if (body.Agent.isStopped) continue;
                double pace = NativeTravelPace.WalkingMetresPerSecond * (body.IdentityId.GetHashCode() % 3 == 0 ? 0.85 : 1);
                var at = body.Position; double d = Position.Distance(at, body.Target), step = pace * seconds;
                body.Character.transform.position = PhysicalPedestrianBody.Vector(d <= step ? body.Target : Position.Lerp(at, body.Target, step / d));
            }
        }
        double minute = now;
        const float frame = 1 / 60f;
        void Frame() { Move(frame); minute += frame / 3.0; city.AdvanceTo(minute); Time.Next(frame); }
        for (int i = 0; i < 120; i++) { Frame(); bodies.Update(); }
        int count = 2400, seen = 0; long bytes = 0;
        var times = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            Frame();
            long a = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            bodies.Update();
            times.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            bytes += GC.GetAllocatedBytesForCurrentThread() - a;
            seen = Math.Max(seen, pool.Active.Count);
        }
        return Stats("walkers 48", times, bytes, seen);
    }

    private static string Cars()
    {
        var ct = typeof(PhysicalCarPerfTests);
        var cityOf = ct.GetMethod("City", BindingFlags.NonPublic | BindingFlags.Static)!;
        var corner = ct.GetMethod("Corner", BindingFlags.NonPublic | BindingFlags.Static)!;
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrians.VisiblePoint = null;
        MultimodalRoutes.CarSamples = true;
        try
        {
            var city = (CitySimulator)cityOf.Invoke(null, null)!;
            NativeTravelPace.Focus = new Position(76, 0, 0); Time.Next(3);
            using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
                typeof(F9gScratchPerf).Assembly, new VendorBodyTests.Log(), 8, 24, 120, new DwellSpots());
            var pool = PhysicalVehicles.Last!;
            double minute = city.State.Minute;
            const float frame = 1 / 60f;
            const double gamePerSecond = 1 / 3.0;
            void Frame()
            {
                foreach (var body in pool.Active.Values) body.Pace = body.CitizenId.GetHashCode() % 3 == 0 ? 0.6 : 1.2;
                pool.Drive(frame, 10 * gamePerSecond, b => (Position?)corner.Invoke(null, new object[] { city, b }));
                minute += frame * gamePerSecond; city.AdvanceTo(minute); Time.Next(frame);
            }
            int warm = 0;
            while (pool.Active.Count < 24 && warm++ < 600) { Frame(); bodies.Update(); }
            int count = 1800, seen = 0; long bytes = 0;
            var times = new List<double>(count);
            for (int i = 0; i < count; i++)
            {
                Frame();
                long a = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                bodies.Update();
                times.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                bytes += GC.GetAllocatedBytesForCurrentThread() - a;
                seen = Math.Max(seen, pool.Active.Count);
            }
            return Stats("cars 24", times, bytes, seen);
        }
        finally { MultimodalRoutes.CarSamples = false; }
    }
}
