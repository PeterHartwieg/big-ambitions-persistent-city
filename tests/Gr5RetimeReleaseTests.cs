using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5RetimeReleaseTests
{
    private sealed class Walk : IRoutePlanner
    {
        public Route Plan(Place a, Place b, double at, TravelPreferences p)
            => new Route { Legs = new() { new RouteLeg { From = a.Position, To = b.Position, Mode = TravelMode.Walk, Minutes = Math.Max(1, Position.Distance(a.Position, b.Position) / 2) } } };
    }

    private static (PhysicalPopulation Bodies, PhysicalPedestrians Pool) Walkers(int walkers) { var w = WalkerCity(walkers); return (w.Bodies, w.Pool); }

    private static (PhysicalPopulation Bodies, PhysicalPedestrians Pool, CitySimulator City, Func<PhysicalPopulation> Rebind) WalkerCity(int walkers)
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear();
        const double now = 1440 + 600;
        var ids = Enumerable.Range(0, walkers).Select(i => new CitizenIdentity { Id = "w" + i.ToString("D2"), Name = "W" + i, Age = 30, AppearanceSeed = i + 1 }).ToList();
        var places = new List<Place> { new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 64, Position = new Position(100, 0, 0) } };
        for (int i = 0; i < walkers; i++)
            places.Add(new Place { Id = "home" + i, Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(100 + 60 * Math.Cos(i * 0.13), 0, 60 * Math.Sin(i * 0.13)) });
        var state = PersistentCity.Core.Population.Create(ids, places, "gr5-release", "0.40.4", 3, now);
        for (int i = 0; i < walkers; i++)
        {
            var c = state.Citizens[i];
            c.HomeId = c.PlaceId = "home" + i; c.DayPlaceId = "work"; c.MealOuting = null;
            c.Phase = CitizenPhase.Travelling; c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
            c.Journey = new Walk().Plan(places[i + 1], places[0], now, c.Preferences);
            c.DepartureMinute = now; c.NextEventMinute = now + c.Journey.Minutes; c.ActivityUntilMinute = now + 600;
        }
        var city = new CitySimulator(state, new Walk());
        NativeTravelPace.Focus = new Position(100, 0, 0); Time.Next(3);
        PhysicalPopulation Rebind() => new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
            typeof(Gr5RetimeReleaseTests).Assembly, new VendorBodyTests.Log(), 64, 8, 120, new DwellSpots());
        var bodies = Rebind();
        var pool = PhysicalPedestrians.Last!;
        double minute = now;
        for (int i = 0; i < 120; i++) { minute += 1 / 180.0; city.AdvanceTo(minute); Time.Next(1 / 60f); bodies.Update(); }
        return (bodies, pool, city, Rebind);
    }

    private static List<(int Bound, int Pooled)> Checkpoints(int walkers, int cycles, bool retime)
    {
        var (bodies, pool, city, rebind) = WalkerCity(walkers);
        var reads = new List<(int, int)> { (pool.Active.Count, pool.Acquisitions.Select(a => a.Actor).Distinct().Count() - pool.Active.Count) };
        double minute = city.State.Minute;
        try
        {
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                if (retime)
                {
                    int updates = 0;
                    while (!bodies.ReleaseForRetime(long.MaxValue)) Assert.True(++updates < 16, "release did not finish");
                    Assert.Empty(pool.Active);
                    bodies.Dispose();
                    bodies = rebind();
                    pool = PhysicalPedestrians.Last!;
                }
                for (int i = 0; i < 120; i++) { minute += 1 / 180.0; city.AdvanceTo(minute); Time.Next(1 / 60f); bodies.Update(); }
                reads.Add((pool.Active.Count, pool.Acquisitions.Select(a => a.Actor).Distinct().Count() - pool.Active.Count));
            }
        }
        finally { bodies.Dispose(); }
        return reads;
    }

    [Fact]
    public void RepeatedRetimeReleasesRebindTheSameBodiesAsAnUnretimedControlAtMatchedCheckpoints()
    {
        const int walkers = 24, cycles = 4;
        var control = Checkpoints(walkers, cycles, retime: false);
        var retimed = Checkpoints(walkers, cycles, retime: true);
        Assert.True(control[0].Bound >= 4, control[0].Bound.ToString());
        for (int i = 0; i <= cycles; i++)
        {
            Assert.Equal(control[i].Bound, retimed[i].Bound);
            Assert.True(retimed[i].Pooled <= control[i].Pooled, "R" + i + " idle pooled bodies " + retimed[i].Pooled + " vs control " + control[i].Pooled);
        }
    }

    [Fact]
    public void ARetimeReleasesBodiesOneSliceAtATimeAndDisposesInALaterUpdate()
    {
        var (bodies, pool) = Walkers(24);
        using (bodies)
        {
            int bound = pool.Active.Count;
            Assert.True(bound >= 4, bound.ToString());
            int calls = 0;
            while (bodies.HasBodies)
            {
                int before = pool.Active.Count;
                Assert.False(bodies.ReleaseForRetime(0));
                calls++;
                Assert.True(pool.Active.Count <= before && pool.Active.Count >= before - 1, before + " -> " + pool.Active.Count);
                Assert.True(calls <= bound + 1);
            }
            Assert.Empty(pool.Active);
            Assert.True(calls >= bound - 1, calls + " slices for " + bound + " bodies");
            int teardown = 0;
            while (!bodies.ReleaseForRetime(0)) Assert.True(++teardown <= 6, teardown + " teardown updates");
            Assert.True(teardown >= 4, "cars, ambient walkers, the car pool and the body pool are torn down in their own updates");
            Assert.True(bodies.ReleaseForRetime(long.MaxValue));
        }
    }

    [Fact]
    public void AnUnboundedSliceReleasesEveryBodyLikeSuspend()
    {
        var (sliced, pool) = Walkers(24);
        using (sliced)
        {
            Assert.True(pool.Active.Count >= 4);
            Assert.False(sliced.ReleaseForRetime(long.MaxValue));
            Assert.False(sliced.HasBodies);
            Assert.Empty(pool.Active);
            int teardown = 0;
            while (!sliced.ReleaseForRetime(long.MaxValue)) teardown++;
            Assert.Equal(3, teardown);
            Assert.True(sliced.ReleasingForRetime);
        }
    }

    [Fact]
    public void AnAmbientRestoreThatNeedsSeveralSlicesHoldsTheReleaseUntilItFinishes()
    {
        var (bodies, _) = Walkers(12);
        using (bodies)
        {
            var ambient = AmbientPedestrians.Last!;
            ambient.RestoreSlices = 3;
            while (bodies.HasBodies) Assert.False(bodies.ReleaseForRetime(long.MaxValue));
            int updates = 0;
            while (ambient.DisposeSlices == 0) { Assert.False(bodies.ReleaseForRetime(long.MaxValue)); updates++; Assert.True(updates < 4); }
            Assert.False(bodies.ReleaseForRetime(long.MaxValue));
            Assert.False(bodies.ReleaseForRetime(long.MaxValue));
            Assert.Equal(3, ambient.DisposeSlices);
            int after = 0;
            while (!bodies.ReleaseForRetime(long.MaxValue)) after++;
            Assert.Equal(2, after);
            Assert.Equal(3, ambient.DisposeSlices);
        }
    }
}
