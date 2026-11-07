using System;
using System.Linq;
using BAModAPI;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class VendorBodyTests
{
    internal sealed class Log : IModLogger
    {
        public void Info(string _) { }
        public void Warn(string _) { }
    }
    internal sealed class Fixture : IDisposable
    {
        internal readonly VehicleMarketTests.Fixture Vendors;
        internal readonly PhysicalPopulation Bodies;
        internal PhysicalPedestrians Pool => PhysicalPedestrians.Last!;
        internal CitySimulator City => Vendors.City;
        internal CitizenState Person => City.State.Citizens[0];
        internal ResidentMeal Meal => City.State.Food!.Residents[0];
        internal VendorRow Row => City.State.Vendors.Find("v00")!;
        internal Fixture(int people = 1, int cap = 8, bool travelling = false, double stayMinutes = 30)
        {
            StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear();
            NativeTravelPace.Focus = new(100, 0, 0); Time.Next(3);
            Vendors = new(); var state = Vendors.City.State; state.Minute = Vendors.Now;
            var original = state.Citizens[0];
            state.Citizens.Clear(); state.Food!.Residents.Clear();
            for (int i = 0; i < people; i++)
            {
                string id = "body" + i.ToString("D2");
                state.Citizens.Add(new CitizenState { Identity = new CitizenIdentity { Id = id, Name = id, Age = 30, AppearanceSeed = i + 1 },
                    HomeId = original.HomeId, ShopId = original.ShopId, DayPlaceId = original.DayPlaceId, LeisureId = original.LeisureId,
                    PlaceId = Row.PlaceId, Activity = Activity.Shop, Phase = CitizenPhase.AtPlace,
                    NextEventMinute = state.Minute + stayMinutes, ActivityUntilMinute = state.Minute + stayMinutes });
                var meal = Vendors.Meal(resident: i); meal.ResidentId = meal.AnchorId = id;
                meal.ServiceAt = state.Minute + 2 + i; meal.DueAt = meal.ServiceAt;
                state.Food.Residents.Add(meal);
            }
            if (travelling)
            {
                var c = state.Citizens[0]; c.PlaceId = c.HomeId; c.Activity = Activity.Home;
                c.DestinationId = Row.PlaceId; c.DestinationActivity = Activity.Shop; c.Phase = CitizenPhase.Travelling;
                c.DepartureMinute = state.Minute; c.NextEventMinute = state.Minute + 1;
                c.Journey = new Route { Legs = { new RouteLeg { From = state.Places.Single(p => p.Id == c.HomeId).Position,
                    To = Row.Stand!.Position, Mode = TravelMode.Walk, Minutes = 1 } } };
                var meal = state.Food.Residents[0]; meal.Status = MealStatus.Travelling; meal.ExpiresAt = state.Minute + 60;
                meal.Deadline = state.Minute + 60; meal.DueAt = state.Minute + 10;
                c.MealOuting = new MealOuting { OriginId = c.HomeId, OriginActivity = Activity.Home, ResumeUntil = state.Minute + 60, ResumeEvent = state.Minute + 60 };
            }
            Vendors.City = new CitySimulator(state, Vendors.Planner); Vendors.Market.Rebind(Vendors.City);
            if (travelling) Vendors.City.AttachFoodMarket(Vendors.Market);
            var parent = new Transform { Component = new PopulationRuntime { vendorMarket = Vendors.Market } };
            Bodies = new(parent, Vendors.City, new MultimodalRoutes(), typeof(Fixture).Assembly, new Log(), cap, 3, 100, new DwellSpots());
        }
        internal void Tick() { Time.Next(); Bodies.Update(); }
        internal void ReachTargets()
        { foreach (var b in Pool.Active.Values) b.Character.transform.position = PhysicalPedestrianBody.Vector(b.Target); Tick(); }
        public void Dispose() { Bodies.Dispose(); Vendors.Market.Dispose(); }
    }

    [Theory]
    [InlineData("cart", 0f, 0.8, 12, 29.6)] [InlineData("cart", 90f, 0.8, 17.6, 24)]
    [InlineData("truck", 180f, 1.0, 12, 17)] [InlineData("truck", 270f, 1.0, 5, 24)]
    public void QueueUsesApiOriginHeightHeadingAndKindSpacing(string kind, float heading, double spacing, double lastX, double lastZ)
    {
        NavMesh.Reset(); Time.Next();
        var row = new VendorRow("body-geometry", kind) { Stand = new VendorStand { OpeningId = 7, Published = true, Position = new(10, 2, 20) } };
        var vehicle = new VendorVehicle { Kind = kind, Open = true, OpeningOwner = VendorNames.Owner, OpeningId = 7,
            QueueX = 12, QueueY = 2, QueueZ = 24, QueueHeading = heading };
        var spots = new DwellSpots(); Assert.True(spots.TryVendorQueue(row, vehicle, out var line));
        Assert.Equal(8, line!.Spots.Count);
        Assert.Equal(row.Stand.Position, line.Entrance); Assert.Equal(line.Entrance, line.Door);
        Assert.Equal(new Position(12, 2, 24), line.Spots[0]);
        // Cardinal fixtures pin actual endpoints, independently of the placement calculation.
        Assert.Equal(lastX, line.Spots[^1].X, 5); Assert.Equal(lastZ, line.Spots[^1].Z, 5);
        Assert.All(line.Spots, p => Assert.Equal(2, p.Y));
        for (int i = 1; i < line.Spots.Count; i++) Assert.Equal(spacing, Position.Distance(line.Spots[i - 1], line.Spots[i]), 5);
        int samples = NavMesh.Samples;
        Assert.True(spots.TryVendorQueue(row, vehicle, out var cached)); Assert.Same(line, cached);
        Assert.Equal(samples, NavMesh.Samples); // no extra native work for an unchanged layout
        var place = new Place { Id = row.PlaceId, Position = row.Stand.Position };
        Assert.Equal(place.Id, DwellSpots.KeyOf(place));
        Assert.False(spots.TryQueue(place, out _)); // no building-door fallback
    }

    [Fact]
    public void UnwalkableOrShiftedQueueStopsShortRatherThanSnappingAcrossRoadOrLevel()
    {
        using var f = new Fixture();
        var vehicle = f.Vendors.Market.Coordinator.Image!.Find("v00")!;
        var spots = new DwellSpots();
        NavMesh.Sample = p => p.x > 102 ? null : p;
        Assert.True(spots.TryVendorQueue(f.Row, vehicle, out var line)); Assert.Equal(2, line!.Spots.Count);
        Time.Next(); var noLine = new DwellSpots();
        NavMesh.Sample = p => p + new Vector3(0.2f, 0, 0);
        Assert.False(noLine.TryVendorQueue(f.Row, vehicle, out _));
        Time.Next(); NavMesh.Sample = p => p; NavMesh.Blocked = (_, _) => true;
        Assert.False(new DwellSpots().TryVendorQueue(f.Row, vehicle, out _));
        Time.Next(); NavMesh.Blocked = null; NavMesh.Sample = p => p + new Vector3(0, 1, 0);
        Assert.False(new DwellSpots().TryVendorQueue(f.Row, vehicle, out _));
    }

    [Fact]
    public void ChangedLayoutInvalidatesCacheAndOnlyOneLineIsSampledPerFrame()
    {
        using var f = new Fixture();
        var vehicle = f.Vendors.Market.Coordinator.Image!.Find("v00")!;
        var spots = new DwellSpots();
        Assert.True(spots.TryVendorQueue(f.Row, vehicle, out var original));
        var row2 = new VendorRow("other", "cart") { Stand = f.Row.Stand };
        Assert.False(spots.TryVendorQueue(row2, vehicle, out _));
        Time.Next(); Assert.True(spots.TryVendorQueue(row2, vehicle, out _));
        vehicle.QueueZ += 4; vehicle.QueueHeading = 0;
        Time.Next(); Assert.True(spots.TryVendorQueue(f.Row, vehicle, out var changed));
        Assert.NotSame(original, changed); Assert.Equal(4, changed!.Spots[0].Z);
        f.Row.Stand!.Published = false;
        Assert.False(spots.TryVendorQueue(f.Row, vehicle, out _));
    }

    [Fact]
    public void SelectedBodyWalksFromStandToQueueAndFacesHatch()
    {
        using var f = new Fixture(); f.Tick();
        var body = Assert.Single(f.Pool.Active).Value;
        Assert.Equal(f.Row.Stand!.Position, body.Position);
        Assert.Equal(new Position(101, 0, 0), body.Target);
        Assert.False(body.Agent.isStopped);
        f.ReachTargets();
        Assert.True(body.Agent.isStopped); Assert.Equal(f.Row.Stand.Position, body.Facing);
        Assert.Equal(f.Person.Identity.Id, body.IdentityId);
        Assert.Same(f.Person.Identity, f.Pool.Acquisitions[^1].Identity);
        int reads = f.Vendors.Api.RevisionReads, copies = f.Vendors.Api.CopyCalls;
        for (int i = 0; i < 4; i++) f.Tick();
        Assert.Equal(reads, f.Vendors.Api.RevisionReads); Assert.Equal(copies, f.Vendors.Api.CopyCalls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BodiesWalkOffQueueWhenStandClosesOrServiceFinishes(bool paid)
    {
        using var f = new Fixture(); f.Tick(); f.ReachTargets();
        var body = Assert.Single(f.Pool.Active).Value;
        if (paid) f.Meal.Status = MealStatus.Paid;
        else { StreetFoodHooks.Close("v00"); f.Vendors.Run(f.Vendors.Now + 1); f.City.State.Minute = f.Vendors.Now; }
        f.Tick();
        Assert.False(body.Agent.isStopped); Assert.Equal(f.Row.Stand!.Position, body.Target);
        f.ReachTargets(); Assert.Empty(f.Pool.Active); Assert.False(body.Valid);
        f.Tick(); Assert.Empty(f.Pool.Active);
    }

    [Theory]
    [InlineData(1)] [InlineData(4)] [InlineData(8)] [InlineData(16)]
    public void VendorBodiesShareExistingCapsAttemptBudgetAndEightPlaceLine(int cap)
    {
        using var f = new Fixture(people: 20, cap: cap);
        AmbientPedestrians.Reserved.Add("body00");
        for (int i = 0; i < 7; i++)
        {
            int before = f.Pool.Active.Count; f.Tick();
            Assert.InRange(f.Pool.Active.Count - before, 0, 4);
            Assert.InRange(f.Pool.Active.Count, 0, Math.Min(8, cap));
        }
        Assert.Equal(Math.Min(8, cap), f.Pool.Active.Count);
        Assert.DoesNotContain("body00", f.Pool.Active.Keys);
        Assert.Equal(f.Pool.Active.Count, f.Pool.Active.Values.Select(b => b.Target).Distinct().Count());
        Assert.False(ActiveJourneyIndex.VisitorsReceiveBodies);
    }

    [Fact]
    public void CameraReleaseAndPoolReuseKeepSavedResidentIdentityAndAppearanceSeed()
    {
        using var f = new Fixture(cap: 1); var identity = f.Person.Identity; int seed = identity.AppearanceSeed;
        f.Tick(); var first = Assert.Single(f.Pool.Active).Value;
        NativeTravelPace.Focus = new(5000, 0, 5000); f.Tick(); Assert.Empty(f.Pool.Active); Assert.False(first.Valid);
        NativeTravelPace.Focus = new(100, 0, 0); Time.Next(3); f.Tick();
        var second = Assert.Single(f.Pool.Active).Value;
        Assert.Same(first.Character, second.Character); Assert.True(second.Generation > first.Generation);
        Assert.Equal(first.IdentityId, second.IdentityId); Assert.Same(identity, f.Person.Identity);
        Assert.Same(identity, f.Pool.Acquisitions[^1].Identity); Assert.Equal(seed, f.Person.Identity.AppearanceSeed);
    }

    [Fact]
    public void RecycledActorReceivesOnlyNextResidentsExistingIdentity()
    {
        using var f = new Fixture(people: 2, cap: 1); f.Tick(); f.ReachTargets();
        var first = Assert.Single(f.Pool.Active).Value; var firstIdentity = f.Person.Identity;
        f.Meal.Status = MealStatus.Paid; f.Tick(); f.ReachTargets();
        var next = Assert.Single(f.Pool.Active).Value;
        Assert.False(first.Valid); Assert.Equal("body00", first.IdentityId); Assert.Equal("body01", next.IdentityId);
        Assert.Same(first.Character, next.Character); Assert.True(next.Generation > first.Generation);
        Assert.Same(firstIdentity, f.Person.Identity);
        Assert.Same(f.City.State.Citizens[1].Identity, f.Pool.Acquisitions[^1].Identity);
        Assert.Equal(1, firstIdentity.AppearanceSeed); Assert.Equal(2, f.City.State.Citizens[1].Identity.AppearanceSeed);
    }

    [Fact]
    public void ArrivingWalkerKeepsItsBodyThroughBudgetedArrivalAndJoinsQueue()
    {
        using var f = new Fixture(travelling: true); f.Tick();
        var body = Assert.Single(f.Pool.Active).Value;
        Assert.Equal(f.Person.Identity.Id, body.IdentityId);
        body.Character.transform.position = PhysicalPedestrianBody.Vector(f.Row.Stand!.Position);
        f.Tick(); // observed corner returns the journey lease; event not processed yet
        Assert.Same(body, Assert.Single(f.Pool.Active).Value);
        Assert.Equal(CitizenPhase.Travelling, f.Person.Phase);
        while (!f.City.AdvanceTo(f.City.State.Minute, 1).CaughtUp) { } // ordinary budgeted arrival
        Assert.Equal(CitizenPhase.AtPlace, f.Person.Phase);
        f.Tick();
        Assert.Same(body, Assert.Single(f.Pool.Active).Value);
        Assert.Equal(new Position(101, 0, 0), body.Target);
        f.ReachTargets(); Assert.True(body.Agent.isStopped);
    }

    [Fact]
    public void DepartureHoldsMeasuredJourneyUntilBodyWalksBackToStand()
    {
        using var f = new Fixture(travelling: true, stayMinutes: 2); f.Tick();
        var body = Assert.Single(f.Pool.Active).Value;
        body.Character.transform.position = PhysicalPedestrianBody.Vector(f.Row.Stand!.Position);
        f.Tick(); while (!f.City.AdvanceTo(f.City.State.Minute, 1).CaughtUp) { } f.Tick(); f.ReachTargets();
        Assert.True(body.Agent.isStopped);
        double leaves = f.Person.NextEventMinute;
        while (!f.City.AdvanceTo(leaves).CaughtUp) { }
        Assert.Equal(CitizenPhase.Travelling, f.Person.Phase);
        f.Tick();
        Assert.Equal(f.Row.Stand.Position, body.Target);
        while (!f.City.AdvanceTo(leaves + 1).CaughtUp) { }
        Assert.Equal(f.Row.Stand.Position, f.City.PositionOf(f.Person));
        Assert.Same(body, Assert.Single(f.Pool.Active).Value);
        f.ReachTargets(); f.Tick();
        Assert.Same(body, Assert.Single(f.Pool.Active).Value);
        Assert.Equal(f.City.State.Places.Single(p => p.Id == f.Person.HomeId).Position, body.Target);
        Assert.Equal(f.Row.Stand.Position, f.City.PositionOf(f.Person));
    }

    [Fact]
    public void BlockedQueueApproachGivesUpWithoutRepeatedBodyRespawns()
    {
        using var f = new Fixture(); f.Tick();
        var body = Assert.Single(f.Pool.Active).Value;
        for (int i = 0; i < 75; i++) f.Tick(); // actor never follows its path
        Assert.Empty(f.Pool.Active); Assert.False(body.Valid);
        int acquisitions = f.Pool.Acquisitions.Count;
        Time.Next(3); f.Tick(); Assert.Empty(f.Pool.Active);
        Assert.Equal(acquisitions, f.Pool.Acquisitions.Count);
    }

    [Fact]
    public void FailedVendorSamplingSharesRetryDelayAndOpeningChangeRetriesFresh()
    {
        using var f = new Fixture(); var spots = new DwellSpots();
        var vehicle = f.Vendors.Market.Coordinator.Image!.Find("v00")!;
        NavMesh.Sample = _ => null;
        Assert.False(spots.TryVendorQueue(f.Row, vehicle, out _)); int sampled = NavMesh.Samples;
        Time.Next(); Assert.False(spots.TryVendorQueue(f.Row, vehicle, out _)); Assert.Equal(sampled, NavMesh.Samples);
        vehicle.OpeningId++; f.Row.Stand!.OpeningId++;
        Time.Next(); NavMesh.Sample = p => p;
        Assert.True(spots.TryVendorQueue(f.Row, vehicle, out _)); Assert.True(NavMesh.Samples > sampled);
    }

    [Fact]
    public void QueueLayoutChangeWalksExistingBodyBackRatherThanMovingItToNewLine()
    {
        using var f = new Fixture(); f.Tick(); f.ReachTargets();
        var body = Assert.Single(f.Pool.Active).Value; var position = body.Position;
        var vehicle = f.Vendors.Market.Coordinator.Image!.Find("v00")!;
        vehicle.QueueZ += 4; f.Tick();
        Assert.Equal(position, body.Position); Assert.Equal(f.Row.Stand!.Position, body.Target);
        Assert.False(body.Agent.isStopped);
    }

    [Fact]
    public void CurrentWriter31GoldenRemainsUnchanged()
    {
        var state = EconomicLedgerTests.Fresh(5150); EconomicLedgerTests.Populate(state);
        Assert.Equal(31, CityState.CurrentSchema);
        Assert.Equal("06b494d2627e0d6675d8c437e1c0dff72e176ab3ae7aaa54a0f152052aace308", EconomicLedgerTests.Sha(SaveCodec.Write(state)));
    }
}
