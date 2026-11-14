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
public sealed class DeferredDressSelectionTests
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
    public void ADeferredBurstNeitherBindsNorScansMoreThanTheLimitAndHitsStillGetBodies()
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrians.VisiblePoint = null;
        const int walkers = 200;
        const double now = 1440 + 600;
        var ids = Enumerable.Range(0, walkers).Select(i => new CitizenIdentity { Id = "d" + i.ToString("D3"), Name = "D" + i, Age = 30, AppearanceSeed = i + 1 }).ToList();
        var places = new List<Place> { new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = walkers, Position = new Position(100, 0, 0) } };
        for (int i = 0; i < walkers; i++)
            places.Add(new Place { Id = "home" + i, Name = "Home", Kind = PlaceKind.Home, Capacity = 1,
                Position = new Position(100 + 60 * Math.Cos(i * 0.031), 0, 60 * Math.Sin(i * 0.031)) });
        var state = Population.Create(ids, places, "deferred-dress", "0.40.4", 3, now);
        for (int i = 0; i < walkers; i++)
        {
            var c = state.Citizens[i];
            c.HomeId = c.PlaceId = "home" + i; c.DayPlaceId = "work"; c.MealOuting = null;
            c.Phase = CitizenPhase.Travelling; c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
            c.Journey = Legs(places[i + 1].Position, places[0].Position);
            c.DepartureMinute = now; c.NextEventMinute = now + c.Journey.Minutes; c.ActivityUntilMinute = now + 600;
        }
        var city = new CitySimulator(state, new Walk());
        NativeTravelPace.Focus = new Position(100, 0, 0); Time.Next(3);
        try
        {
            PhysicalPedestrians.Defer = _ => true;
            using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
                typeof(DeferredDressSelectionTests).Assembly, new VendorBodyTests.Log(), 96, 8, 120, new DwellSpots());
            var pool = PhysicalPedestrians.Last!;
            int preflight = pool.AcquireCalls, preflightBodies = pool.Acquisitions.Count;
            double minute = now;
            int passes = 0;
            for (int i = 0; i < 60; i++)
            {
                int checks = pool.DeferChecks;
                long deferred = pool.AppearanceDeferred;
                minute += 0.3 / 60; city.AdvanceTo(minute); Time.Next(0.3f);
                bodies.Update();
                int scanned = pool.DeferChecks - checks;
                Assert.Equal(scanned, pool.AppearanceDeferred - deferred);
                Assert.InRange(scanned, 0, PhysicalPopulation.DressScanLimit);
                if (scanned > 0) passes++;
            }
            Assert.True(passes >= 30);
            Assert.True(pool.AppearanceDeferred >= passes);
            Assert.Equal(preflight, pool.AcquireCalls);
            Assert.Empty(pool.Active);

            PhysicalPedestrians.Defer = identity => identity.AppearanceSeed % 2 == 0;
            for (int i = 0; i < 40; i++)
            {
                minute += 0.3 / 60; city.AdvanceTo(minute); Time.Next(0.3f);
                bodies.Update();
            }
            Assert.NotEmpty(pool.Active);
            Assert.All(pool.Active.Values, body => Assert.True(ids.First(x => x.Id == body.IdentityId).AppearanceSeed % 2 == 1));
            Assert.Equal(pool.AcquireCalls - preflight, pool.Acquisitions.Count - preflightBodies);
        }
        finally { PhysicalPedestrians.Defer = null; }
    }

    [Fact]
    public void IdleTrimmingIsTriedOnEveryFrameEvenWhenEveryFrameRunsSelection()
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrians.VisiblePoint = null;
        const double now = 1440 + 600;
        var ids = Enumerable.Range(0, 8).Select(i => new CitizenIdentity { Id = "t" + i, Name = "T" + i, Age = 30, AppearanceSeed = i + 1 }).ToList();
        var places = new List<Place> { new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 8, Position = new Position(100, 0, 0) } };
        for (int i = 0; i < 8; i++)
            places.Add(new Place { Id = "home" + i, Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(100 + 40 * Math.Cos(i), 0, 40 * Math.Sin(i)) });
        var state = Population.Create(ids, places, "trim-at-4fps", "0.40.4", 3, now);
        var city = new CitySimulator(state, new Walk());
        NativeTravelPace.Focus = new Position(100, 0, 0); Time.Next(3);
        using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
            typeof(DeferredDressSelectionTests).Assembly, new VendorBodyTests.Log(), 96, 8, 120, new DwellSpots());
        var pool = PhysicalPedestrians.Last!;
        double minute = now;
        const int frames = 40;
        int before = pool.TrimCalls;
        for (int i = 0; i < frames; i++)
        {
            minute += 0.25 / 60; city.AdvanceTo(minute); Time.Next(0.25f);
            bodies.Update();
        }
        Assert.Equal(frames, pool.TrimCalls - before);
    }
}
