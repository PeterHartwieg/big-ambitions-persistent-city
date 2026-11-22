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

    private static CitySimulator Walkers(double now)
    {
        const int walkers = 24;
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
        return new CitySimulator(state, new Walk());
    }

    [Fact]
    public void AReservedUpdateKeepsBoundWalkersFollowingButStartsNoSelectionParkedScanOrIdleTrim()
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrians.VisiblePoint = null;
        const double now = 1440 + 600;
        var city = Walkers(now);
        NativeTravelPace.Focus = new Position(100, 0, 0); Time.Next(3);
        try
        {
            using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
                typeof(Gr5FrameDeferralTests).Assembly, new VendorBodyTests.Log(), 96, 8, 120, new DwellSpots());
            var pool = PhysicalPedestrians.Last!;
            int preflight = pool.AcquireCalls;
            double minute = now;
            for (int i = 0; i < 8 && bodies.Followers == 0; i++)
            {
                minute += 0.3 / 60; city.AdvanceTo(minute); Time.Next(0.3f);
                Frame(0);
                bodies.Update();
            }
            Assert.True(pool.AcquireCalls > preflight && bodies.Followers > 0, "light frames bind a walker " + bodies.Diagnostics);
            int calls = pool.AcquireCalls, parked = bodies.ParkedScans, bound = bodies.BoundBodies;
            for (int i = 0; i < 12; i++)
            {
                minute += 0.3 / 60; city.AdvanceTo(minute); Time.Next(0.3f);
                Frame(i % 2 == 0 ? 0 : FrameDeferral.DefaultFrameMilliseconds + 1);
                UpdateCityClock.Reserve();
                int trims = pool.TrimCalls;
                bodies.Update();
                Assert.Equal(trims, pool.TrimCalls);
                Assert.Equal(city.View.Minute, pool.Minute);
            }
            Assert.Equal(calls, pool.AcquireCalls);
            Assert.Equal(parked, bodies.ParkedScans);
            Assert.Equal(bound, bodies.BoundBodies);
            Assert.True(bodies.Followers > 0, "reserved updates keep following " + bodies.Diagnostics);
            Frame(0);
            Assert.False(UpdateCityClock.Reserved);
            for (int i = 0; i < 4 && pool.AcquireCalls == calls; i++)
            {
                minute += 0.3 / 60; city.AdvanceTo(minute); Time.Next(0.3f);
                Frame(0);
                bodies.Update();
            }
            Assert.True(pool.AcquireCalls > calls, "the next unreserved light frame selects again " + bodies.Diagnostics);
        }
        finally { UpdateCityClock.Reset(); }
    }

    [Fact]
    public void ThePublicationUpdateReservesItselfBeforePhysicalWork()
    {
        string source = System.IO.File.ReadAllText(System.IO.Path.Combine(CapabilityReportTests.Root(), "Runtime", "PersistentCityMod.cs"));
        int reserve = source.IndexOf("if (publishedNetwork) UpdateCityClock.Reserve();", StringComparison.Ordinal);
        int published = source.IndexOf("bool publishedNetwork = simulation.NetworkPublished;", StringComparison.Ordinal);
        int physicalRun = source.IndexOf("double physicalStarted = UpdateCityClock.Elapsed();", StringComparison.Ordinal);
        Assert.True(published >= 0 && reserve > published && physicalRun > reserve);
    }

    [Fact]
    public void PhysicalSelectionAndParkedScansNeverRunOnAHeavyUpdateEvenAfterSecondsOfHeavyUpdates()
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
            int calls = pool.AcquireCalls, parked = bodies.ParkedScans;
            double minute = now;
            for (int i = 0; i < 12; i++)
            {
                minute += 0.3 / 60; city.AdvanceTo(minute); Time.Next(0.3f);
                Frame(FrameDeferral.DefaultFrameMilliseconds + 1);
                bodies.Update();
            }
            Assert.Equal(calls, pool.AcquireCalls);
            Assert.Equal(parked, bodies.ParkedScans);
            Assert.Contains("longest wait 3.", bodies.Diagnostics);
            for (int i = 0; i < 4 && pool.AcquireCalls == calls; i++)
            {
                minute += 0.3 / 60; city.AdvanceTo(minute); Time.Next(0.3f);
                Frame(0);
                bodies.Update();
            }
            Assert.True(pool.AcquireCalls > calls, "light frames select " + bodies.Diagnostics);
        }
        finally { UpdateCityClock.Reset(); }
    }

    [Fact]
    public void OptionalPhysicalWorkWaitsForALightFrameHoweverLongTheHeavyStreakLasts()
    {
        var gate = new FrameDeferral();
        Assert.True(gate.Allows(3, 0));
        for (int frame = 0; frame < 600; frame++) Assert.False(gate.Allows(8 + frame % 20, 1 + frame / 60.0));
        Assert.Equal(600, gate.Deferred);
        Assert.True(gate.LongestWaitSeconds >= 9.9);
        Assert.True(gate.Allows(7.9, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameDeferral(0));
    }

    [Fact]
    public void ParkedAndSelectionWorkShareOneFrameClock()
    {
        var parkedGate = new FrameDeferral();
        var selectionGate = new FrameDeferral();
        double frame = 5;
        Assert.True(parkedGate.Allows(frame, 0));
        frame += 4;
        Assert.False(selectionGate.Allows(frame, 0));
        Assert.True(selectionGate.Spent(frame));
        Assert.True(selectionGate.Allows(5, 0.02));
    }
}
