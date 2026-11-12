using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.MovementEquivalence;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>GR-9 (G6, G7): the mod's own kinematic cars, parked cars as objects and appearance from the person ID.
/// Offline only; the in-game checks (fades without popping, parked cars across reload, no borrowed car, no hook) are
/// batched for the rig.</summary>
[Collection(StreetFoodHooks.Collection)]
public sealed class OwnCarTests
{
    private static PhysicalTrafficCommandLimit Limit(double advance, bool hold = false) => new(new Position(1, 0, 0), advance, hold);

    /// <summary>The own car never moves past its command limit or its corner, stands while held, and reaches a reachable
    /// corner exactly instead of creeping towards it.</summary>
    [Fact]
    public void OwnCarStaysWithinItsLimitAndLandsOnTheCorner()
    {
        double along = 0, speed = 0, cap = 30;
        for (int frame = 0; frame < 600; frame++)
        {
            var step = OwnCarMotion.Step(along, 40, speed, Limit(cap - along), 1 / 60.0);
            Assert.True(step.Along <= cap + 1e-12 && step.Moved >= 0 && step.Speed <= OwnCarMotion.CruiseMetresPerSecond + 1e-12);
            Assert.True(step.Speed - speed <= OwnCarMotion.AccelerationMetresPerSecondSquared / 60.0 + 1e-12);
            along = step.Along; speed = step.Speed;
        }
        Assert.Equal(cap, along, 9);
        Assert.Equal(0, speed);
        // With room past the corner it stops on the corner and reports it.
        along = 35; speed = 5;
        OwnCarStep last = default;
        for (int frame = 0; frame < 600 && !last.AtCorner; frame++)
        {
            last = OwnCarMotion.Step(along, 40, speed, Limit(100), 1 / 60.0);
            along = last.Along; speed = last.Speed;
        }
        Assert.True(last.AtCorner); Assert.Equal(40, along, 12);
        // A hold stands at once; bad input never moves the car.
        var held = OwnCarMotion.Step(10, 40, 9, Limit(100, hold: true), 1 / 60.0);
        Assert.Equal((10.0, 0.0, 0.0), (held.Along, held.Speed, held.Moved));
        Assert.Equal(0, OwnCarMotion.Step(10, 40, 9, Limit(double.NaN), 1 / 60.0).Moved);
        Assert.Equal(0, OwnCarMotion.Step(10, 40, 9, Limit(5), 0).Moved);
    }

    private static MovementOptions Seeded(CarBodies cars, Action<MovementOptions>? change = null)
    {
        var options = SelfChecks.Small("gr9-" + cars, 96);
        options.Bodies.Cars = cars;
        change?.Invoke(options);
        return options;
    }

    /// <summary>Plan test 1: over a seeded day a journey gives the same legs, times and arrival with own cars as with
    /// borrowed cars, and both equal the city without bodies, on every projection (save, sales, journeys, parked cars,
    /// wallets, FIFO). Own cars did drive.</summary>
    [Fact]
    public void OwnCarsGiveTheSameJourneysAsBorrowedCarsOverASeededDay()
    {
        var plain = MovementRun.Run(Seeded(CarBodies.Borrowed, o => o.Driver = false));
        var borrowed = MovementRun.Run(Seeded(CarBodies.Borrowed));
        var own = MovementRun.Run(Seeded(CarBodies.Own));
        Assert.True(own.Metrics["bodies.carFrames"] > 0);
        Assert.True(own.Metrics["journeys.car.count"] > 0);
        var compared = Equivalence.Compare(own, borrowed);
        Assert.True(compared.Identical, string.Join(", ", compared.Equal.Where(p => !p.Value).Select(p => p.Key)));
        Assert.True(SelfChecks.Same(own, plain));
        foreach (var key in new[] { "journeys.car.meanMinutes", "journeys.car.p95Minutes", "journeys.arrivals", "cars.parkedAway" })
            Assert.Equal(borrowed.Metrics[key], own.Metrics[key]);
    }

    /// <summary>Plan test 2 (harness): with own cars, person and car IDs and parked positions survive pool reuse (bodies
    /// released and re-acquired as the camera sweeps away and back), a mid-journey reload and camera movement: the run
    /// equals its bodies-off reference on every projection.</summary>
    [Fact]
    public void OwnCarIdsSurvivePoolReuseReloadAndCameraMovement()
    {
        var plain = MovementRun.Run(Seeded(CarBodies.Own, o => { o.Driver = false; o.ReloadMidJourney = true; }));
        var own = MovementRun.Run(Seeded(CarBodies.Own, o => { o.Focus = FocusPath.AwayReturn; o.ReloadMidJourney = true; }));
        Assert.Equal(1, own.Reloads);
        Assert.True(own.Metrics["bodies.released"] > 0 && own.Metrics["bodies.acquired"] > own.Metrics["bodies.maxCars"]);
        Assert.True(SelfChecks.Same(own, plain));
    }

    private sealed class NoRoutes : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences) => null;
    }
    private static readonly Position Home = new(100, 0, 0);
    private static CityState ParkedCity(int people)
    {
        var ids = Enumerable.Range(0, people).Select(i => new CitizenIdentity { Id = "p" + i, Name = "P" + i, Age = 30, AppearanceSeed = 7 }).ToList();
        var state = Population.Create(ids, new[] {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = people, Position = Home },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = people, Position = new Position(2000, 0, 0) } }, "gr9", "0.40.4", 5, 1440 + 600);
        for (int i = 0; i < state.Citizens.Count; i++)
        {
            var c = state.Citizens[i];
            c.HomeId = c.PlaceId = "home"; c.DayPlaceId = "work"; c.MealOuting = null; c.Phase = CitizenPhase.AtPlace; c.Activity = Activity.Home;
            c.Preferences.OwnsCar = true; c.NextEventMinute = 1440 * 3; c.ActivityUntilMinute = 1440 * 3;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(100 + 6 * i, 0, 8) };
        }
        return state;
    }

    private static Dictionary<string, Position> ShowParked(CityState state, Position focus)
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrians.VisiblePoint = null;
        NativeTravelPace.Focus = focus; Time.Next(3);
        var city = new CitySimulator(state, new NoRoutes());
        using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
            typeof(OwnCarTests).Assembly, new VendorBodyTests.Log(), 8, 4, 120, new DwellSpots());
        Time.Next(2); bodies.Update();
        Assert.True(PhysicalVehicles.Last!.ParkedRefreshes > 0);
        return PhysicalVehicles.Last!.Parked.ToDictionary(p => p.Key, p =>
        {
            Assert.Equal(p.Key, p.Value.State!.Id);
            Assert.Equal(p.Value.Car.OwnerId + ":car", p.Key);
            return p.Value.Car.At;
        });
    }

    /// <summary>Plan test 2 (runtime): parked cars stand at their owners' saved curb positions, nearest first within the
    /// vehicle cap; moving the camera away and back and reloading the save shows the same cars at the same places, and
    /// showing them writes nothing.</summary>
    [Fact]
    public void ParkedCarsStandAtTheSavedCurbAcrossCameraMovementAndReload()
    {
        var state = ParkedCity(6);
        string before = SaveCodec.Write(state);
        var first = ShowParked(state, Home);
        Assert.Equal(4, first.Count); // the vehicle cap
        Assert.Equal(new[] { "p0:car", "p1:car", "p2:car", "p3:car" }, first.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (var (id, at) in first) Assert.Equal(state.Citizens.Single(c => c.Car!.Id == id).Car!.ParkedPosition, at);
        Assert.Empty(ShowParked(state, new Position(5000, 0, 0)));
        var restored = SaveWire.Read(before).State!;
        Assert.Equal(first, ShowParked(restored, Home));
        Assert.Equal(before, SaveCodec.Write(state));
        Assert.Equal(before, SaveCodec.Write(restored));
    }

    /// <summary>A car inside its owner's car block is driving or being parked, not standing; before it, it stands at the
    /// saved curb, after it at the last car leg's end.</summary>
    [Fact]
    public void ACarIsParkedOutsideItsCarBlockOnly()
    {
        var person = ParkedCity(1).Citizens[0];
        Position curb = person.Car!.ParkedPosition, end = new(300, 0, 0);
        person.Phase = CitizenPhase.Travelling;
        person.Journey = new Route { Legs = new() {
            new RouteLeg { From = Home, To = curb, Minutes = 1, Mode = TravelMode.Walk },
            new RouteLeg { From = curb, To = end, Minutes = 4, Mode = TravelMode.Car },
            new RouteLeg { From = end, To = new Position(310, 0, 0), Minutes = 2, Mode = TravelMode.Walk } } };
        Assert.True(ParkedCars.TryParked(person, 0.5, out var at)); Assert.Equal(curb, at);
        Assert.False(ParkedCars.TryParked(person, 3, out _));
        Assert.True(ParkedCars.TryParked(person, 6, out at)); Assert.Equal(end, at);
        person.Preferences.OwnsCar = false;
        Assert.False(ParkedCars.TryParked(person, 0.5, out _));
    }

    /// <summary>Plan test 3: appearance is reproducible from the person ID: the same identity gives the same look after a
    /// save round trip; two people sharing a donor seed get different looks; every wardrobe is worn.</summary>
    [Fact]
    public void AppearanceIsReproducibleFromThePersonId()
    {
        var state = ParkedCity(400);
        var looks = state.Citizens.ToDictionary(c => c.Identity.Id, c => PersonAppearance.For(c.Identity));
        var restored = SaveWire.Read(SaveCodec.Write(state)).State!;
        foreach (var c in restored.Citizens) Assert.Equal(looks[c.Identity.Id], PersonAppearance.For(c.Identity));
        Assert.Equal(looks["p3"], PersonAppearance.For(new CitizenIdentity { Id = "p3", AppearanceSeed = 7 }));
        // All 400 share donor seed 7, yet their looks differ.
        Assert.True(looks.Values.Select(l => l.Seed).Distinct().Count() > 390);
        Assert.All(looks.Values, l => Assert.True(l.Seed > 0));
        var worn = looks.Values.GroupBy(l => l.Wardrobe).ToDictionary(g => g.Key, g => g.Count());
        foreach (Wardrobe wardrobe in Enum.GetValues(typeof(Wardrobe))) Assert.True(worn.ContainsKey(wardrobe), wardrobe.ToString());
        Assert.True(worn[Wardrobe.Casual] > worn[Wardrobe.Formal] && worn[Wardrobe.Formal] > worn[Wardrobe.Party]);
    }

    /// <summary>Fade curves: fade-in rises from 0 to 1, a handoff fade-out falls from 1 to 0, both clamped.</summary>
    [Fact]
    public void FadesRiseAndFallWithinTheirDurations()
    {
        Assert.Equal(0f, BodyFade.In(0)); Assert.Equal(1f, BodyFade.In(BodyFade.FadeInSeconds)); Assert.Equal(1f, BodyFade.In(99));
        Assert.InRange(BodyFade.In(BodyFade.FadeInSeconds / 2), 0.49f, 0.51f);
        Assert.Equal(1f, BodyFade.Out(0)); Assert.Equal(0f, BodyFade.Out(BodyFade.HandoffFadeSeconds)); Assert.Equal(0f, BodyFade.Out(-1 + 99));
        Assert.Equal(1f, BodyFade.In(double.NaN)); Assert.Equal(0f, BodyFade.Out(double.NaN));
    }

    private sealed class Copy { public string Model = ""; public bool Live = true; }

    /// <summary>Review Sol-r1-4: cycling through more models than the pool holds never exhausts it. At capacity an idle copy
    /// of another model is destroyed to make room; destroyed copies leave the count; new copies are budgeted per frame.</summary>
    [Fact]
    public void ThePoolEvictsIdleCopiesOfOtherModelsAndBudgetsCreation()
    {
        var destroyed = new List<Copy>();
        var pool = new OwnCarPool<Copy>(1, c => c.Live, c => { c.Live = false; destroyed.Add(c); }, createsPerFrame: 1);
        int frame = 0;
        foreach (var model in new[] { "A", "B", "C", "A" })
        {
            var copy = pool.Take(model, ++frame, () => new Copy { Model = model });
            Assert.NotNull(copy); Assert.Equal(model, copy!.Model); Assert.Equal(1, pool.Count);
            pool.Return(model, copy);
        }
        Assert.Equal(new[] { "A", "B", "C" }, destroyed.Select(c => c.Model));
        // The idle copy of the requested model is reused without creating.
        var again = pool.Take("A", ++frame, () => throw new InvalidOperationException("must reuse"));
        Assert.Equal("A", again!.Model);
        // A copy destroyed outside the pool leaves the count; the budget allows one new copy this frame, not two.
        again.Live = false;
        Assert.Equal(0, pool.Count);
        var budget = new OwnCarPool<Copy>(8, c => c.Live, c => c.Live = false, createsPerFrame: 1);
        Assert.NotNull(budget.Take("A", 7, () => new Copy()));
        Assert.Null(budget.Take("B", 7, () => new Copy()));
        Assert.NotNull(budget.Take("B", 8, () => new Copy()));
        Assert.Equal(2, budget.Count);
        // At capacity with every copy in use nothing is evicted.
        var full = new OwnCarPool<Copy>(1, c => c.Live, c => c.Live = false);
        Assert.NotNull(full.Take("A", 1, () => new Copy()));
        Assert.Null(full.Take("B", 2, () => new Copy()));
    }

    /// <summary>Review Sol-r1-3: a car held (a correction fade) right after a corner keeps its yaw, so its root, which pivots
    /// on the front axle, stands still; only a moving car turns, at the turn rate.</summary>
    [Fact]
    public void AHeldCarAfterATurnKeepsItsPose()
    {
        double yaw = 0, target = 90;
        Assert.Equal(yaw, OwnCarMotion.Yaw(yaw, target, 1 / 60.0, hold: true));
        var front = new Position(10, 0, 5); var offset = new Position(0, 0, 2);
        Position Root(double y) => PhysicalVehicleReference.Root(front, offset, new Position(Math.Sin(y * Math.PI / 180), 0, Math.Cos(y * Math.PI / 180)));
        var before = Root(yaw);
        for (int frame = 0; frame < 60; frame++) yaw = OwnCarMotion.Yaw(yaw, target, 1 / 60.0, hold: true);
        Assert.Equal(before, Root(yaw));
        // Moving, it turns at most TurnDegreesPerSecond a second, the short way round.
        Assert.Equal(3, OwnCarMotion.Yaw(0, 90, 1 / 60.0, hold: false), 9);
        Assert.Equal(-3, OwnCarMotion.Yaw(0, 270, 1 / 60.0, hold: false), 9);
        Assert.Equal(90, OwnCarMotion.Yaw(89, 90, 1 / 60.0, hold: false), 9);
        // A held command step does not move the car either.
        Assert.Equal(0, OwnCarMotion.Step(4, 12, 6, Limit(8, hold: true), 1 / 60.0).Moved);
    }

    /// <summary>Review Opus-r1-1: the own car's safety only shortens the kernel's limit: it stops a gap short of an obstacle
    /// ahead and at its corner while the native light there is red.</summary>
    [Fact]
    public void ObstaclesAndRedLightsShortenTheLimit()
    {
        Assert.Equal(10, OwnCarMotion.Safe(Limit(10), null, false, 5).MaximumAdvance);
        Assert.Equal(4 - OwnCarMotion.ObstacleGapMetres, OwnCarMotion.Safe(Limit(10), 4, false, 5).MaximumAdvance, 12);
        Assert.Equal(0, OwnCarMotion.Safe(Limit(10), 0.5, false, 5).MaximumAdvance);
        Assert.Equal(5, OwnCarMotion.Safe(Limit(10), null, true, 5).MaximumAdvance);
        Assert.Equal(2, OwnCarMotion.Safe(Limit(2), 30, true, 5).MaximumAdvance);
        Assert.True(OwnCarMotion.Safe(Limit(10, hold: true), 0, true, 0).HoldPosition);
        Assert.True(OwnCarMotion.LookAheadMetres(OwnCarMotion.CruiseMetresPerSecond) > 16);
        // A car approaching an obstacle 20 m ahead at cruise stops a gap short of it, never reaching it.
        double along = 0, speed = OwnCarMotion.CruiseMetresPerSecond, obstacle = 20;
        for (int frame = 0; frame < 600; frame++)
        {
            var step = OwnCarMotion.Step(along, 100, speed, OwnCarMotion.Safe(Limit(100), obstacle - along, false, 100 - along), 1 / 60.0);
            along = step.Along; speed = step.Speed;
        }
        Assert.Equal(obstacle - OwnCarMotion.ObstacleGapMetres, along, 6);
    }

    /// <summary>Review Sol-r2-1: the whole body counts, not the front axle. A 4 m by 1.8 m car with its front axle 2 m
    /// ahead of its root, standing at a corner and turning right, swings its rear left: an obstacle just beside the rear is
    /// clear at the current pose but overlaps after the first 3-degree step, so that move is refused.</summary>
    [Fact]
    public void AnObstacleBesideATurningCarBlocksTheTurn()
    {
        var front = new Position(0, 0, 0); var offset = new Position(0, 0, 2);
        var obstacle = new CarRectangle(new Position(-1.1, 0, -3.9), 0, 0.3, 0.1);
        var now = OwnCarFootprint.At(front, 0, offset, 4, 1.8);
        Assert.False(OwnCarFootprint.Overlaps(now, obstacle));
        double yaw = OwnCarMotion.Yaw(0, 90, 1 / 60.0, hold: false);
        Assert.Equal(3, yaw, 9);
        Assert.True(OwnCarFootprint.Overlaps(OwnCarFootprint.At(front, yaw, offset, 4, 1.8), obstacle));
        // The front-axle slice alone misses it: a thin box at the axle, 3 degrees on, does not reach the rear.
        Assert.False(OwnCarFootprint.Overlaps(new CarRectangle(front, yaw, 0.25, 0.9), obstacle));
        // A car driving straight past the same obstacle at 1 m clearance never touches it.
        var beside = new CarRectangle(new Position(-2.0, 0, -3.9), 0, 0.3, 0.1);
        for (double z = 0; z < 10; z += 0.2) Assert.False(OwnCarFootprint.Overlaps(OwnCarFootprint.At(new Position(0, 0, z), 0, offset, 4, 1.8), beside));
    }
}
