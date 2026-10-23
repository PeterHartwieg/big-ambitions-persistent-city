using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalTrafficEndpointReconciliationTests
{
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly double[] X = { 220, 226.99713134765625, 233.34124755859375, 240.2172393798828 };
    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; } = new TrafficNetwork(Native, X.Select((x, i) => new TrafficNetworkNode
        { Id = i, Position = new Position(x,0,0), KilometresPerHour = 30, AllowsCar = true, Boundary = i == 2,
            Successors = i + 1 < X.Length ? new[] { i + 1 } : Array.Empty<int>() }).ToArray(), 1);
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
    }

    private sealed class Fixture
    {
        public readonly Planner Planner = new Planner();
        public readonly CitySimulator Simulation;
        public readonly CitizenState Citizen;
        public readonly PhysicalJourneyLease Lease;
        public readonly Route Road;
        public readonly TrafficRouteLeg[] Mapped;
        public Position Corner => new Position(X[2],0,0);

        public Fixture()
        {
            // Actual route search and the actual network mapper supply all timings/section spans.
            var graph = new TransportRoadGraph(X.Select((x, i) => new TransportRoadNode
            { Position = new Position(x,0,0), KilometresPerHour = 30, AllowsCar = true,
                Successors = i + 1 < X.Length ? new[] { i + 1 } : Array.Empty<int>() }).ToArray());
            Road = graph.Plan(0, 3, _ => false, (_, _) => true)!;
            for (int i = 0; i < Road.Legs.Count; i++) Road.Legs[i].ServiceId = $"native:road:v1:{Native}:{i}:{i + 1}";
            Assert.True(Planner.TrafficNetwork!.TryMap(Road, out var mapped));
            Mapped = mapped.ToArray(); Assert.Equal(2, Mapped.Length);
            var state = Population.Create(new[] { new CitizenIdentity { Id = "proof", Name = "Proof", Age = 30, Persona = Persona.Commuter } }, new[]
            {
                new Place { Id = "a", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = Road.Legs[0].From },
                new Place { Id = "b", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = Road.Legs[2].To }
            }, "negative-endpoint-proof", "0.40.4", 1, 100);
            Citizen = state.Citizens[0];
            Citizen.PlaceId = Citizen.HomeId = "a"; Citizen.DayPlaceId = Citizen.DestinationId = "b";
            Citizen.Phase = CitizenPhase.Travelling; Citizen.DestinationActivity = Activity.Work; Citizen.ActivityUntilMinute = 3000;
            Citizen.DepartureMinute = 100; Citizen.Preferences.OwnsCar = true;
            Citizen.Car = new CarState { Id = "proof:car", ParkedPosition = Road.Legs[0].From };
            Citizen.Journey = Road; Citizen.NextEventMinute = 100 + Road.Minutes;
            Simulation = new CitySimulator(state, Planner); Simulation.AdvanceTo(100);
            Assert.True(Simulation.TryAcquirePhysicalJourney(Citizen.Identity.Id, out var lease)); Lease = lease!;
            Simulation.AdvanceTo(110);
        }

        public PhysicalJourneySample PrepareMiddleLeg()
        {
            Assert.True(Simulation.TrySamplePhysicalJourney(Lease, out var first));
            Assert.True(PhysicalProgress.TryProject(first, Road.Legs[0].To, true, out double firstEnd));
            Assert.True(Simulation.ObservePhysicalJourney(Lease, firstEnd));
            Assert.True(Simulation.TrySamplePhysicalJourney(Lease, out var middle));
            Assert.True(PhysicalProgress.TryProject(middle, Position.Lerp(Road.Legs[1].From, Road.Legs[1].To, .5), false, out double measured));
            Assert.True(Simulation.ObservePhysicalJourney(Lease, measured));
            Assert.True(Simulation.TrySamplePhysicalJourney(Lease, out middle));
            Assert.Equal(1, middle.LegIndex);
            Assert.Equal(1.220302734375, middle.ElapsedMinutes);
            return middle;
        }

        public PhysicalJourneySample SeedLegacyUnderreportedBoundary()
        {
            var previous = PrepareMiddleLeg();
            Assert.True(PhysicalProgress.TryProject(previous, Corner, true, out double endpoint));
            Assert.Equal(1.6009497070312497, endpoint); Assert.True(endpoint < Mapped[0].EndNominalMinutes);
            Assert.True(Simulation.ObservePhysicalJourney(Lease, endpoint));
            Assert.True(Simulation.TrySamplePhysicalJourney(Lease, out var next));
            Assert.Equal(2, next.LegIndex); Assert.Equal(0, next.LegFraction); Assert.Equal(endpoint, next.ElapsedMinutes);
            return next;
        }
    }

    [Fact]
    public void ActualMappedNegativeEndpointReconcilesOnlyToItsAlreadyAllowedEndBeforeTransfer()
    {
        var f = new Fixture(); var sample = f.PrepareMiddleLeg();
        var permission = f.Simulation.Traffic!.GetPermission(f.Citizen.Identity.Id);
        Assert.Equal(1.60094970703125, permission.MaximumNominalMinutes);
        Assert.True(PhysicalProgress.TryProject(sample, f.Corner, true, out double projected));
        Assert.True(projected < permission.MaximumNominalMinutes);
        Assert.True(PhysicalTrafficCornerPermission.TryReconcileEndpoint(sample, true, projected, permission.MaximumNominalMinutes, out double accepted));
        Assert.Equal(permission.MaximumNominalMinutes, accepted);
        Assert.False(PhysicalTrafficCornerPermission.TryReconcileEndpoint(sample, false, projected, permission.MaximumNominalMinutes, out _));
        Assert.True(f.Simulation.ObservePhysicalJourney(f.Lease, accepted));
        Assert.Equal(permission.LinkId, f.Simulation.Traffic.GetPermission(f.Citizen.Identity.Id).LinkId); // Reporting alone never transfers ownership.
        f.Simulation.AdvanceTo(f.Simulation.State.Minute);
        Assert.Equal(f.Mapped[1].LinkId, f.Simulation.Traffic.GetPermission(f.Citizen.Identity.Id).LinkId);
        Assert.Equal(accepted, f.Lease.ObservedMinutes);

        int completions = 0;
        f.Simulation.Traffic.VehicleCompleted += (id, _) => { Assert.Equal(f.Citizen.Identity.Id, id); completions++; };
        f.Simulation.AdvanceTo(111);
        Assert.True(f.Simulation.IsPhysicalJourneyValid(f.Lease));
        Assert.True(f.Simulation.TrySamplePhysicalJourney(f.Lease, out var finalSample));
        Assert.Equal(2, finalSample.LegIndex);
        var terminalPermission = f.Simulation.Traffic.GetPermission(f.Citizen.Identity.Id);
        Assert.True(terminalPermission.PhysicalControl); Assert.False(terminalPermission.IsCompleted);
        Assert.Equal(f.Road.Minutes, terminalPermission.MaximumNominalMinutes);
        Assert.True(PhysicalProgress.TryProject(finalSample, f.Road.Legs[2].To, true, out double terminalProjected));
        Assert.True(PhysicalTrafficCornerPermission.TryReconcileEndpoint(finalSample, true, terminalProjected,
            terminalPermission.MaximumNominalMinutes, out double terminalAccepted));
        Assert.Equal(f.Road.Minutes, terminalAccepted);
        Assert.True(f.Simulation.ObservePhysicalJourney(f.Lease, terminalAccepted));
        Assert.Equal(terminalAccepted, f.Lease.ObservedMinutes);
        Assert.True(f.Simulation.Traffic.GetPermission(f.Citizen.Identity.Id).PhysicalControl);
        Assert.Equal(0, completions); Assert.Equal(0, f.Citizen.CompletedJourneys); // The report does not process a terminal event.
        f.Simulation.AdvanceTo(111);
        Assert.Equal(1, completions); Assert.Equal(1, f.Citizen.CompletedJourneys);
        Assert.Equal(f.Road.Legs[2].To, f.Citizen.Car!.ParkedPosition);
        Assert.Equal(0, f.Simulation.Traffic.VehicleCount);
        Assert.All(f.Simulation.Traffic.Capture().Links, link => Assert.Empty(link.Vehicles));
        Assert.False(f.Simulation.IsPhysicalJourneyValid(f.Lease));
        Assert.False(f.Simulation.ObservePhysicalJourney(f.Lease, terminalAccepted));
        f.Simulation.AdvanceTo(112); f.Simulation.AdvanceTo(113);
        Assert.Equal(1, completions); Assert.Equal(1, f.Citizen.CompletedJourneys);
    }

    [Fact]
    public void UnchangedCollinearLegacyBoundaryCanRecoverWithoutANewNativeReceipt()
    {
        var f = new Fixture(); var next = f.SeedLegacyUnderreportedBoundary();
        for (int frame = 0; frame < 100; frame++)
        {
            f.Simulation.AdvanceTo(111 + frame);
            Assert.True(PhysicalProgress.TryProject(next, f.Corner, false, out double unchanged));
            Assert.True(f.Simulation.ObservePhysicalJourney(f.Lease, unchanged));
            Assert.True(f.Simulation.TrySamplePhysicalJourney(f.Lease, out next));
            var stalled = f.Simulation.Traffic!.GetPermission(f.Citizen.Identity.Id);
            Assert.Equal(f.Mapped[0].LinkId, stalled.LinkId); Assert.True(stalled.CanCrossBoundary);
            Assert.True(stalled.NominalMinutes < stalled.MaximumNominalMinutes);
            Assert.Equal(0, PhysicalTrafficMotion.MaximumAdvance(next, f.Corner, stalled.MaximumNominalMinutes));
        }
        double maximum = f.Simulation.Traffic!.GetPermission(f.Citizen.Identity.Id).MaximumNominalMinutes;
        Assert.True(PhysicalTrafficCornerPermission.TryReconcileNormalizedBoundary(next, f.Lease.ObservedMinutes,
            next.ElapsedMinutes, maximum, out double accepted));
        Assert.Equal(maximum, accepted);
        Assert.True(f.Simulation.ObservePhysicalJourney(f.Lease, accepted)); f.Simulation.AdvanceTo(f.Simulation.State.Minute);
        Assert.Equal(f.Mapped[1].LinkId, f.Simulation.Traffic.GetPermission(f.Citizen.Identity.Id).LinkId);
    }

    [Fact]
    public void CopiedSavedUnderreportedBoundaryReacquiresAndRecoversItsOwnSectionOnly()
    {
        var f = new Fixture(); f.SeedLegacyUnderreportedBoundary();
        var read = SaveCodec.Read(SaveCodec.Write(f.Simulation.State));
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var copied = new CitySimulator(read.State!, new Planner());
        Assert.True(copied.TryAcquirePhysicalJourney(f.Citizen.Identity.Id, out var lease));
        copied.AdvanceTo(copied.State.Minute + 1);
        Assert.True(copied.TrySamplePhysicalJourney(lease!, out var sample));
        var before = copied.Traffic!.GetPermission(f.Citizen.Identity.Id);
        Assert.Equal(f.Mapped[0].LinkId, before.LinkId);
        Assert.True(PhysicalTrafficCornerPermission.TryReconcileNormalizedBoundary(sample, lease!.ObservedMinutes,
            sample.ElapsedMinutes, before.MaximumNominalMinutes, out double accepted));
        Assert.Equal(before.MaximumNominalMinutes, accepted);
        Assert.True(copied.ObservePhysicalJourney(lease, accepted)); copied.AdvanceTo(copied.State.Minute);
        Assert.Equal(f.Mapped[1].LinkId, copied.Traffic.GetPermission(f.Citizen.Identity.Id).LinkId);
        Assert.Equal(f.Mapped[0].LinkId, f.Simulation.Traffic!.GetPermission(f.Citizen.Identity.Id).LinkId); // Original copied world was not changed.
    }

    [Fact]
    public void ExactReconciledEndpointDoesNotBypassFullReceivingStorageOrRestoreOwnership()
    {
        var f = new Fixture(); var sample = f.PrepareMiddleLeg();
        var kernel = new TrafficKernel(f.Planner.TrafficNetwork!.Links);
        kernel.Enqueue("blocker", new[] { new TrafficRouteLeg { LinkId = f.Mapped[1].LinkId, StartNominalMinutes = 0,
            EndNominalMinutes = f.Road.Legs[2].Minutes, FreeFlowMinutes = f.Road.Legs[2].Minutes } });
        kernel.Enqueue("head", f.Mapped); kernel.AdvanceTo(0, 100);
        kernel.SetPhysicalControl("head", true); kernel.SetPhysicalControl("blocker", true); kernel.AdvanceTo(100, 100);
        Assert.False(kernel.GetPermission("head").CanCrossBoundary);
        Assert.True(PhysicalProgress.TryProject(sample, f.Corner, true, out double projected));
        Assert.True(PhysicalTrafficCornerPermission.TryReconcileEndpoint(sample, true, projected,
            kernel.GetPermission("head").MaximumNominalMinutes, out double accepted));
        Assert.Equal(accepted, kernel.ReportPhysicalProgress("head", accepted)); kernel.AdvanceTo(200, 100);
        var copy = TrafficKernel.Restore(kernel.Capture(), false); copy.AdvanceTo(300, 100);
        Assert.Equal(f.Mapped[0].LinkId, copy.GetPermission("head").LinkId);
        Assert.Equal(accepted, copy.GetPermission("head").NominalMinutes);
        Assert.Equal(f.Mapped[1].LinkId, copy.GetPermission("blocker").LinkId);
        Assert.All(copy.Capture().Links, link => Assert.Single(link.Vehicles));
    }

    [Fact]
    public void ReconciliationCannotGrowAnUnmeasuredMovementBudget()
    {
        var f = new Fixture(); var next = f.SeedLegacyUnderreportedBoundary(); double observed = f.Lease.ObservedMinutes;
        Assert.False(PhysicalTrafficCornerPermission.TryReconcileNormalizedBoundary(next, observed, next.ElapsedMinutes + 1e-6, observed + 1e-6, out _));
        Assert.False(PhysicalTrafficCornerPermission.TryReconcileNormalizedBoundary(next, observed, next.ElapsedMinutes, observed + 1e-6, out _));
        Assert.False(PhysicalTrafficCornerPermission.TryReconcileNormalizedBoundary(next, observed, next.ElapsedMinutes, Math.BitDecrement(observed), out _));
        var previous = f.Simulation.SampleJourney(f.Citizen, 1.220302734375);
        double endpoint = previous.ElapsedMinutes + previous.RemainingLegMinutes;
        Assert.False(PhysicalTrafficCornerPermission.TryReconcileEndpoint(previous, true, endpoint - 1e-10, endpoint, out _));
        Assert.False(PhysicalTrafficCornerPermission.TryReconcileEndpoint(previous, true, endpoint, endpoint + 1e-8, out _));
    }
}
