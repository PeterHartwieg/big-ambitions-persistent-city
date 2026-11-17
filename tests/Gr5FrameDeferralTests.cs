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
public sealed class Gr5FrameDeferralTests
{
    private sealed class Walk : IRoutePlanner
    {
        public Route Plan(Place a, Place b, double at, TravelPreferences p) => new Route { Legs = new() {
            new RouteLeg { From = a.Position, To = b.Position, Mode = TravelMode.Walk, Minutes = Position.Distance(a.Position, b.Position) / 2 } } };
    }

    private static void Frame(double spentMilliseconds)
    {
        UpdateCityClock.Begin();
        while (UpdateCityClock.Elapsed() < spentMilliseconds) { }
    }

    [Fact]
    public void PhysicalSelectionWaitsWhileTheUpdateIsAlreadyHeavyAndRunsOnTheNextLightOrOverdueFrame()
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrians.VisiblePoint = null;
        const int walkers = 24;
        const double now = 1440 + 600;
        var ids = Enumerable.Range(0, walkers).Select(i => new CitizenIdentity { Id = "g" + i.ToString("D2"), Name = "G" + i, Age = 30, AppearanceSeed = i + 1 }).ToList();
        var places = new List<Place> { new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = walkers, Position = new Position(100, 0, 0) } };
        for (int i = 0; i < walkers; i++)
            places.Add(new Place { Id = "home" + i, Name = "Home", Kind = PlaceKind.Home, Capacity = 1,
                Position = new Position(100 + 60 * Math.Cos(i * 0.2), 0, 60 * Math.Sin(i * 0.2)) });
        var state = Population.Create(ids, places, "frame-deferral", "0.40.4", 3, now);
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
        try
        {
            using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
                typeof(Gr5FrameDeferralTests).Assembly, new VendorBodyTests.Log(), 96, 8, 120, new DwellSpots());
            var pool = PhysicalPedestrians.Last!;
            int calls = pool.AcquireCalls;
            double minute = now;
            for (int i = 0; i < 3; i++)
            {
                minute += 0.3 / 60; city.AdvanceTo(minute); Time.Next(0.3f);
                Frame(FrameDeferral.DefaultFrameMilliseconds + 1);
                bodies.Update();
            }
            Assert.Equal(calls, pool.AcquireCalls);
            minute += 0.5 / 60; city.AdvanceTo(minute); Time.Next(0.5f);
            Frame(FrameDeferral.DefaultFrameMilliseconds + 1);
            bodies.Update();
            Assert.True(pool.AcquireCalls > calls, "an overdue heavy frame still selects");
            calls = pool.AcquireCalls;
            minute += 0.3 / 60; city.AdvanceTo(minute); Time.Next(0.3f);
            Frame(0);
            bodies.Update();
            Assert.True(pool.AcquireCalls > calls, "a light frame selects");
        }
        finally { UpdateCityClock.Reset(); }
    }

    [Fact]
    public void OptionalPhysicalWorkWaitsForALighterFrameButNeverLongerThanItsMaximum()
    {
        var gate = new FrameDeferral();
        Assert.True(gate.Allows(3, 0));
        Assert.False(gate.Allows(8, 1));
        Assert.False(gate.Allows(25, 1.5));
        Assert.True(gate.Allows(7.9, 1.6));
        Assert.Equal(2, gate.Deferred);
        for (int frame = 0; frame < 60; frame++) Assert.False(gate.Allows(12, 2 + frame / 60.0));
        Assert.True(gate.Allows(12, 3));
        Assert.Equal(1, gate.Forced);
        Assert.False(gate.Allows(12, 3.1));
    }

    [Fact]
    public void AHeavyStreetFrameStopsBeforeTheSpawnThatWouldCrossThirtyMilliseconds()
    {
        const double prelude = 12.0, grow = 22.0;
        var gate = new FrameDeferral();
        double frame = prelude;
        int spawned = 0;
        for (double second = 0; second < 0.9; second += 1 / 60.0)
        {
            if (!gate.Allows(frame, second)) continue;
            frame += grow; spawned++;
        }
        Assert.Equal(0, spawned);
        double light = 4;
        Assert.True(gate.Allows(light, 1));
        Assert.True(light + grow < 30);
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameDeferral(0));
    }
}
