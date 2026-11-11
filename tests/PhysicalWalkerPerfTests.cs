using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Tests;

// Opt-in offline component timing for docs/perf/f2c.md: set PC_F2C_MEASURE to an output file. Uses only API that
// also exists before F2c, so the same file times the base build (walk leases) and this one (walker follow).
[Collection(StreetFoodHooks.Collection)]
public sealed class PhysicalWalkerPerfTests
{
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

    [Fact]
    public void MeasureUpdateWithFortyEightWalkers()
    {
        string? path = PerfGate.Variable("PC_F2C_MEASURE");
        if (path == null) return;
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear();
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
            c.Journey = Legs(places[i + 1].Position, places[0].Position);
            c.DepartureMinute = now; c.NextEventMinute = now + c.Journey.Minutes; c.ActivityUntilMinute = now + 600;
        }
        var city = new CitySimulator(state, new Walk());
        NativeTravelPace.Focus = new Position(100, 0, 0); Time.Next(3);
        using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
            typeof(PhysicalWalkerPerfTests).Assembly, new VendorBodyTests.Log(), 64, 8, 120, new DwellSpots());
        var pool = PhysicalPedestrians.Last!;
        // Native movement double: a walking body steps toward its target at native pace, slowed for a seeded third of them.
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
        int count = 2400;
        var times = new List<double>(count); int bodiesSeen = 0;
        for (int i = 0; i < count; i++)
        {
            Frame();
            long start = Stopwatch.GetTimestamp();
            bodies.Update();
            times.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            bodiesSeen = Math.Max(bodiesSeen, pool.Active.Count);
        }
        times.Sort();
        File.AppendAllText(path, string.Format(CultureInfo.InvariantCulture,
            "Update, 48 travelling walkers: bound max={0}, n={1}, p50={2:F4} ms, p95={3:F4} ms, max={4:F4} ms, spikes>=30ms={5}{6}",
            bodiesSeen, count, times[count / 2], times[(int)Math.Ceiling(count * .95) - 1], times[^1], times.Count(t => t >= 30), System.Environment.NewLine));
    }
}
