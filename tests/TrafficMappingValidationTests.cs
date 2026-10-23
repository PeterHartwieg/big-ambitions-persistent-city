using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficMappingValidationTests
{
    private const string NativeId = "mapping-native";
    private static Position P(double x, double z = 0) => new(x, 0, z);
    private static TrafficNetworkNode Node(int id, Position position, params int[] next) => new()
    { Id = id, Position = position, AllowsCar = true, KilometresPerHour = 36, Successors = next };
    private static TrafficNetwork Straight() => new(NativeId, new[]
    { Node(0, P(0), 1), Node(1, P(10), 2), Node(2, P(20)) }, 1);
    private static RouteLeg Leg(Position from, Position to, double minutes, string tag = "") => new()
    { From = from, To = to, Minutes = minutes, Mode = TravelMode.Car, ServiceId = tag };
    private static string Tag(int from, int to) => "native:road:v1:" + NativeId + ":" + from + ":" + to;
    private static string Fragment(int from, int to) => "native:road:fragment1:" + NativeId + ":" + from + ":" + to;
    private static Route Journey(params RouteLeg[] legs) => new() { Legs = legs.ToList() };
    private static TrafficRouteLeg Span(int link, double start, double end, double spatialStart = 0, double spatialEnd = 1) => new()
    { LinkId = link, StartNominalMinutes = start, EndNominalMinutes = end, StartLinkFraction = spatialStart, EndLinkFraction = spatialEnd };

    [Fact]
    public void TaggedCornersAndSeveralSectionsValidateAgainstCommittedGeometry()
    {
        var network = new TrafficNetwork(NativeId, new[]
        { Node(0, P(0), 1), Node(1, P(10), 2), Node(2, P(10, 10), 3), Node(3, P(20, 10)) }, 1);
        var journey = Journey(Leg(P(0), P(10), 1, Tag(0, 1)), Leg(P(10), P(10, 10), 1, Tag(1, 2)),
            Leg(P(10, 10), P(20, 10), 1, Tag(2, 3)));
        Assert.True(network.TryMap(journey, out var mapped));
        network.ValidateMapping(journey, mapped);
    }

    [Fact]
    public void AValidSectionIdFromAnotherRoadCannotBorrowTheJourneysNominalOffsets()
    {
        var network = new TrafficNetwork(NativeId, new[]
        { Node(0, P(0), 1), Node(1, P(10)), Node(2, P(0, 5), 3), Node(3, P(10, 5)) }, 1);
        var journey = Journey(Leg(P(0), P(10), 1, Tag(0, 1)));
        Assert.True(network.TryMap(journey, out var mapped));
        Assert.True(network.TryGetEdge(2, 3, out var other));
        mapped[0].LinkId = other.LinkId;
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(journey, mapped));
    }

    [Fact]
    public void WrongDirectionAndWrongFullSectionFractionsAreRejected()
    {
        TrafficNetwork network = Straight();
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(
            Journey(Leg(P(20), P(10), 1), Leg(P(10), P(0), 1)), new[] { Span(0, 0, 2) }));
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(
            Journey(Leg(P(0), P(10), 1), Leg(P(10), P(20), 1)), new[] { Span(0, 0, 2, 0.1, 0.9) }));
    }

    [Fact]
    public void UntaggedChordCannotSkipAnIntermediateNativeTurn()
    {
        var network = new TrafficNetwork(NativeId, new[]
        { Node(0, P(0), 1), Node(1, P(10), 2), Node(2, P(10, 10)) }, 1);
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(
            Journey(Leg(P(0), P(10, 10), 2)), new[] { Span(0, 0, 2) }));
        network.ValidateMapping(Journey(Leg(P(0), P(10), 1), Leg(P(10), P(10, 10), 1)), new[] { Span(0, 0, 2) });
    }

    [Fact]
    public void DisconnectedFragmentsAndCollinearBacktrackingCannotProveTraversal()
    {
        TrafficNetwork network = Straight();
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(
            Journey(Leg(P(0), P(5), 1), Leg(P(7), P(20), 1)), new[] { Span(0, 0, 2) }));
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(
            Journey(Leg(P(0), P(10), 1), Leg(P(10), P(5), 1), Leg(P(5), P(20), 1)), new[] { Span(0, 0, 3) }));
    }

    [Fact]
    public void ClippedSourceAndDestinationInsideNativeEdgesRemainValid()
    {
        TrafficNetwork network = Straight();
        Route journey = Journey(Leg(P(0), P(10), 1, Tag(0, 1)), Leg(P(10), P(20), 1, Tag(1, 2)));
        network.ValidateMapping(journey, new[] { Span(0, 0.5, 1.5, 0.25, 0.75) });
        network.ValidateMapping(journey, new[] { Span(0, 0.5, 2, 0.25, 1) });
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(journey, new[] { Span(0, 0.5, 1.5, 0, 0.75) }));
    }

    [Fact]
    public void LegacyUntaggedSplitFragmentsCanValidateIndividualGeometryClaims()
    {
        TrafficNetwork network = Straight();
        // Original edge 0->1 was split at x=3. Its completed prefix keeps .3 min,
        // its remaining movement doubles, and the next original native edge doubles.
        Route retimed = Journey(Leg(P(0), P(3), 0.3), Leg(P(3), P(10), 1.4), Leg(P(10), P(20), 2, Tag(1, 2)));
        network.ValidateMapping(retimed, new[] { Span(0, 0.3, 3.7, 0.15, 1) });
        network.ValidateMapping(retimed, new[] { Span(0, 0, 3.7) });
        retimed.Legs[1].To = P(10, 1);
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(retimed, new[] { Span(0, 0.3, 3.7, 0.15, 1) }));
    }

    [Fact]
    public void RetainedNativeTagsMustMatchTheirExactEdgeEvenWithinTheSameSection()
    {
        TrafficNetwork network = Straight();
        var journey = Journey(Leg(P(0), P(10), 1, Tag(1, 2)), Leg(P(10), P(20), 1, Tag(1, 2)));
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(journey, new[] { Span(0, 0, 2) }));
        journey.Legs[0].ServiceId = "native:road:v1:wrong-revision:0:1";
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(journey, new[] { Span(0, 0, 2) }));
    }

    [Fact]
    public void WalkingAndStationaryWaitsCannotMasqueradeAsTrafficGeometry()
    {
        TrafficNetwork network = Straight();
        Route walking = Journey(Leg(P(0), P(10), 1), Leg(P(10), P(20), 1));
        walking.Legs[0].Mode = TravelMode.Walk;
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(walking, new[] { Span(0, 0, 2) }));
        Route wait = Journey(Leg(P(0), P(10), 1), Leg(P(10), P(10), 1), Leg(P(10), P(20), 1));
        Assert.Throws<ArgumentException>(() => network.ValidateMapping(wait, new[] { Span(0, 0, 3) }));
    }

    [Fact]
    public void BindingUsesImmutableGeometryCapturedWhenNetworkWasBuilt()
    {
        var snapshot = new[] { Node(0, P(0), 1), Node(1, P(10), 2), Node(2, P(20)) };
        var network = new TrafficNetwork(NativeId, snapshot, 1);
        snapshot[1].Position = P(300, 300);
        snapshot[0].Successors[0] = 2;
        var route = Journey(Leg(P(0), P(10), 1, Tag(0, 1)), Leg(P(10), P(20), 1, Tag(1, 2)));
        var spans = new[] { Span(0, 0, 2) };
        network.ValidateMapping(route, spans);
        Assert.Equal(0, spans[0].StartNominalMinutes);
        Assert.Equal(2, route.Minutes);
    }

    [Fact]
    public void RemainingMappingClipsTheCurrentTaggedEdgeAndIgnoresCompletedTagHistory()
    {
        TrafficNetwork network = Straight();
        Route route = Journey(Leg(P(0), P(10), 1, Tag(0, 1)), Leg(P(10), P(20), 1, Tag(1, 2)));
        Assert.True(network.TryMapRemaining(route, 0.5, out var mapped));
        TrafficRouteLeg span = Assert.Single(mapped);
        Assert.Equal(0.5, span.StartNominalMinutes);
        Assert.Equal(0.25, span.StartLinkFraction);
        Assert.Equal(1.5, span.FreeFlowMinutes);
        network.ValidateMapping(route, mapped);
        route.Legs[0].ServiceId = "native:road:v1:obsolete-revision:0:1";
        Assert.False(network.TryMap(route, out _));
        Assert.True(network.TryMapRemaining(route, 1, out mapped));
        Assert.Equal(1, Assert.Single(mapped).StartNominalMinutes);
        network.ValidateMapping(route, mapped);
        Assert.False(network.TryMapRemaining(route, 2, out mapped));
        Assert.Empty(mapped);
        Assert.False(network.TryMapRemaining(route, 3, out mapped));
        Assert.Empty(mapped);
    }

    [Fact]
    public void RepeatedClockRetimeInsideCoalescedSectionPreservesOwnedAndRemainingMappings()
    {
        var network = new TrafficNetwork(NativeId, new[]
        { Node(0, P(0), 1), Node(1, P(10), 2), Node(2, P(20), 3), Node(3, P(30)) }, 1);
        Route journey = Journey(Leg(P(0), P(10), 1, Tag(0, 1)), Leg(P(10), P(20), 1, Tag(1, 2)),
            Leg(P(20), P(30), 1, Tag(2, 3)));
        Assert.True(network.TryMap(journey, out var originalMapping));
        Assert.Single(originalMapping);
        var state = Population.Create(new[] { new CitizenIdentity { Id = "retime", Name = "Retime", Age = 30 } }, new[]
        {
            new Place { Id = "home", Kind = PlaceKind.Home, Capacity = 1, Position = P(0) },
            new Place { Id = "work", Kind = PlaceKind.Work, Capacity = 1, Position = P(30) }
        }, "mapping-retime", "0.40.4", 7, 100);
        CitizenState citizen = state.Citizens[0];
        citizen.PlaceId = "home"; citizen.DestinationId = "work"; citizen.Phase = CitizenPhase.Travelling;
        citizen.DepartureMinute = 100; citizen.NextEventMinute = 103; citizen.ActivityUntilMinute = 200;
        citizen.Journey = journey; citizen.Preferences.OwnsCar = true;
        citizen.Car = new CarState { Id = "retime:car", ParkedPosition = P(0) };
        var kernel = new TrafficKernel(network.Links, 100);
        kernel.Enqueue("retime", originalMapping);
        kernel.AdvanceTo(101.5, 100);
        state.Minute = 101.5; state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        state.Traffic = kernel.Capture(); state.TrafficTopologyId = network.TopologyId;
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Route retimed = citizen.Journey!;
        Assert.Equal(4, retimed.Legs.Count);
        Assert.Equal(Fragment(1, 2), retimed.Legs[1].ServiceId);
        Assert.Equal(Fragment(1, 2), retimed.Legs[2].ServiceId);
        Assert.True(network.TryMap(retimed, out _)); // Explicit fragments remain contiguous native road geometry.
        network.ValidateMapping(retimed, state.Traffic!.Vehicles[0].Route);
        var retimedNetwork = new TrafficNetwork(NativeId, new[]
        { Node(0, P(0), 1), Node(1, P(10), 2), Node(2, P(20), 3), Node(3, P(30)) }, 2);
        retimedNetwork.ValidateRemainingMapping(retimed, state.Traffic.Vehicles[0].Route, 1.5);
        Assert.True(network.TryMapRemaining(retimed, 1.5, out var remaining));
        TrafficRouteLeg future = Assert.Single(remaining);
        Assert.Equal(1.5, future.StartNominalMinutes);
        Assert.Equal(0.5, future.StartLinkFraction, 8);
        Assert.Equal(15, retimed.PositionAt(future.StartNominalMinutes).X);
        Assert.Equal(15, retimed.PositionAt(1.5).X); // Current native fragment retains road ownership.
        network.ValidateMapping(retimed, remaining);
        Assert.True(TravelTiming.Apply(state, 2, 0.5));
        var twiceRetimedNetwork = new TrafficNetwork(NativeId, new[]
        { Node(0, P(0), 1), Node(1, P(10), 2), Node(2, P(20), 3), Node(3, P(30)) }, 0.5);
        twiceRetimedNetwork.ValidateRemainingMapping(citizen.Journey!, state.Traffic!.Vehicles[0].Route, 1.5);
    }


    private static TrafficNetwork SeparateSections(double rate = 1)
    {
        var nodes = new[] { Node(0, P(0), 1), Node(1, P(10), 2), Node(2, P(20), 3), Node(3, P(30)) };
        foreach (TrafficNetworkNode node in nodes) node.Boundary = true;
        return new TrafficNetwork(NativeId, nodes, rate);
    }

    private static Route ThreeSectionJourney(double middleMinutes = 1) => Journey(
        Leg(P(0), P(10), 1, Tag(0, 1)), Leg(P(10), P(20), middleMinutes, Tag(1, 2)),
        Leg(P(20), P(30), 1, Tag(2, 3)));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RemainingOwnershipRejectsOmittedMiddleOrTailDespiteValidIndividualClaims(int omitted)
    {
        TrafficNetwork network = SeparateSections();
        Route route = ThreeSectionJourney();
        Assert.True(network.TryMap(route, out var mapped));
        network.ValidateRemainingMapping(route, mapped, 0);
        mapped.RemoveAt(omitted);
        network.ValidateMapping(route, mapped); // Individual claims are valid; future ownership is incomplete.
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapped, 0));
    }

    [Fact]
    public void AWholeNativeSectionCannotHideInsideNominalRoundoffTolerance()
    {
        TrafficNetwork network = SeparateSections();
        Route route = ThreeSectionJourney(Math.Pow(2, -40));
        Assert.True(network.TryMap(route, out var mapped));
        mapped.RemoveAt(1);
        network.ValidateMapping(route, mapped);
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapped, 0));
    }

    [Fact]
    public void WholeOmittedTailIsRejectedEvenWhenItsNominalDurationIsTiny()
    {
        TrafficNetwork network = SeparateSections();
        Route route = ThreeSectionJourney();
        route.Legs[2].Minutes = 1e-14;
        Assert.True(network.TryMap(route, out var mapped));
        mapped.RemoveAt(2);
        network.ValidateMapping(route, mapped);
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapped, 0));
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void ForgedNativeTraversalDurationCannotAccelerateAValidSection(double duration)
    {
        TrafficNetwork network = SeparateSections();
        Route route = ThreeSectionJourney();
        Assert.True(network.TryMap(route, out var mapped));
        mapped[1].FreeFlowMinutes = duration;
        network.ValidateMapping(route, mapped);
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapped, 0));
    }

    [Fact]
    public void PartialSectionsUseNativeDurationTimesSpatialCoverageAndAcceptOneUlpDurationNoise()
    {
        TrafficNetwork network = Straight();
        Route route = Journey(Leg(P(0), P(10), 1, Tag(0, 1)), Leg(P(10), P(20), 1, Tag(1, 2)));
        var partial = Span(0, 0.5, 1.5, 0.25, 0.75);
        partial.FreeFlowMinutes = 1;
        // At the last claimed point, the remaining native tail still needs ownership.
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, new[] { partial }, 0.5));
        var remaining = Span(0, 0.5, 2, 0.25, 1);
        remaining.FreeFlowMinutes = Math.BitIncrement(1.5);
        network.ValidateRemainingMapping(route, new[] { remaining }, 0.5);
        remaining.FreeFlowMinutes = 0.75;
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, new[] { remaining }, 0.5));
    }

    [Fact]
    public void TinyRemainingFractionAndOneUlpNominalGapRemainValidWithoutCoveringAWholeMissingEdge()
    {
        TrafficNetwork network = Straight();
        Route route = Journey(Leg(P(0), P(10), 1, Tag(0, 1)), Leg(P(10), P(20), 1, Tag(1, 2)));
        double progress = Math.BitDecrement(2);
        Assert.True(network.TryMapRemaining(route, progress, out var tiny));
        network.ValidateRemainingMapping(route, tiny, progress);

        network = SeparateSections();
        route = ThreeSectionJourney();
        route.Legs.Insert(0, new RouteLeg { From = P(0), To = P(0), Mode = TravelMode.Walk, Minutes = 1000000 });
        Assert.True(network.TryMap(route, out var mapped));
        mapped[1].StartNominalMinutes = Math.BitIncrement(mapped[1].StartNominalMinutes);
        network.ValidateRemainingMapping(route, mapped, 1000000);
    }

    [Fact]
    public void HistoricalCompletedDurationsMayRetainOldClockRateButFutureDurationsMustMatchNewNetwork()
    {
        TrafficNetwork network = SeparateSections(2);
        Route route = ThreeSectionJourney(2);
        route.Legs[2].Minutes = 2;
        Assert.True(network.TryMap(route, out var mapped));
        Assert.Equal(1, mapped[0].FreeFlowMinutes); // Completed section keeps its earlier clock rate.
        network.ValidateRemainingMapping(route, mapped, 1);
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapped, 0.5));
    }

    [Fact]
    public void NativeDurationEvidenceIsImmutableAndInvalidProgressIsRejected()
    {
        TrafficNetwork network = SeparateSections();
        Route route = ThreeSectionJourney();
        Assert.True(network.TryMap(route, out var mapped));
        network.Links[1].FreeFlowMinutes = 0.001;
        mapped[1].FreeFlowMinutes = 0.001;
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapped, 0));
        mapped[1].FreeFlowMinutes = 1;
        network.ValidateRemainingMapping(route, mapped, 0);
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapped, double.NaN));
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapped, -1));
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapped, 3.1));
    }


    [Fact]
    public void AWholeEdgeWhoseTimeDisappearsAtALargeNominalOffsetIsRejected()
    {
        TrafficNetwork network = SeparateSections();
        Route route = ThreeSectionJourney();
        route.Legs[0].Minutes = 1e-20;
        route.Legs.Insert(0, new RouteLeg { From = P(0), To = P(0), Mode = TravelMode.Walk, Minutes = 1000000 });
        Assert.False(network.TryMap(route, out _));
        Assert.False(network.TryMapRemaining(route, 1000000, out _));
        var claimed = Span(1, Math.BitIncrement(1000000), 1000001);
        claimed.FreeFlowMinutes = 1;
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, new[] { claimed }, 1000000));
    }


    [Fact]
    public void NativeFragmentsMapContiguouslyAndClipByTheirOriginalEdgeCoordinates()
    {
        TrafficNetwork network = Straight();
        Route route = Journey(Leg(P(0), P(3), 0.3, Fragment(0, 1)), Leg(P(3), P(10), 0.7, Fragment(0, 1)),
            Leg(P(10), P(20), 1, Tag(1, 2)));
        Assert.True(network.TryMap(route, out var complete));
        Assert.Single(complete);
        network.ValidateRemainingMapping(route, complete, 0);
        Assert.True(network.TryMapRemaining(route, 0.5, out var remaining));
        TrafficRouteLeg current = Assert.Single(remaining);
        Assert.Equal(0.25, current.StartLinkFraction, 8);
        Assert.Equal(1.5, current.FreeFlowMinutes, 8);
        network.ValidateRemainingMapping(route, remaining, 0.5);
    }

    [Theory]
    [InlineData("reverse")]
    [InlineData("off-edge")]
    [InlineData("wrong-edge")]
    [InlineData("missing-id")]
    public void FragmentProvenanceCannotAuthorizeReversalOffRoadGeometryOrAnotherEdge(string defect)
    {
        TrafficNetwork network = Straight();
        Route route = Journey(Leg(P(3), P(7), 0.4, Fragment(0, 1)));
        TrafficRouteLeg claim = Span(0, 0, 0.4, 0.15, 0.35);
        claim.FreeFlowMinutes = 0.4;
        network.ValidateRemainingMapping(route, new[] { claim }, 0);
        if (defect == "reverse") { route.Legs[0].From = P(7); route.Legs[0].To = P(3); }
        if (defect == "off-edge") route.Legs[0].To = P(7, 1);
        if (defect == "wrong-edge") route.Legs[0].ServiceId = Fragment(1, 2);
        if (defect == "missing-id") route.Legs[0].ServiceId = "native:road:fragment1:" + NativeId + ":0:99";
        Assert.False(network.TryMap(route, out _));
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, new[] { claim }, 0));
    }

    [Fact]
    public void TruncatedFinalRetimedFragmentStillRequiresItsFullNativeRoadTail()
    {
        var nodes = new[] { Node(0, P(0), 1), Node(1, P(12)) };
        nodes[1].Signal = new TrafficSignalSchedule { CycleMinutes = 10, GreenDurationMinutes = 0 };
        var network = new TrafficNetwork(NativeId, nodes, 1);
        Route journey = Journey(Leg(P(0), P(12), 1.2, Tag(0, 1)));
        Assert.True(network.TryMap(journey, out var original));
        var state = Population.Create(new[] { new CitizenIdentity { Id = "final", Age = 30 } }, new[]
        {
            new Place { Id = "home", Kind = PlaceKind.Home, Capacity = 1, Position = P(0) },
            new Place { Id = "work", Kind = PlaceKind.Work, Capacity = 1, Position = P(12) }
        }, "fragment-final", "0.40.4", 7, 1440);
        CitizenState citizen = state.Citizens[0];
        citizen.PlaceId = "home"; citizen.DestinationId = "work"; citizen.Phase = CitizenPhase.Travelling;
        citizen.DepartureMinute = 1440; citizen.NextEventMinute = 1441.2; citizen.ActivityUntilMinute = 2880;
        citizen.Journey = journey; citizen.Preferences.OwnsCar = true;
        citizen.Car = new CarState { Id = "final:car", ParkedPosition = P(0) };
        var kernel = new TrafficKernel(network.Links, 1440);
        kernel.Enqueue("final", original); kernel.AdvanceTo(1440.6, 100);
        state.Minute = kernel.Clock; state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        state.Traffic = kernel.Capture(); state.TrafficTopologyId = network.TopologyId;
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.All(citizen.Journey!.Legs, leg => Assert.Equal(Fragment(0, 1), leg.ServiceId));
        nodes[1].Signal!.CycleMinutes = 20;
        var newNetwork = new TrafficNetwork(NativeId, nodes, 2);
        var saved = state.Traffic!.Vehicles[0];
        double progress = TrafficKernel.Restore(state.Traffic, false).GetPermission("final").NominalMinutes;
        newNetwork.ValidateRemainingMapping(citizen.Journey, saved.Route, progress);
        TrafficRouteLeg shortened = saved.Route[0];
        shortened.EndNominalMinutes = (shortened.StartNominalMinutes + shortened.EndNominalMinutes) / 2;
        shortened.EndLinkFraction = (shortened.StartLinkFraction + shortened.EndLinkFraction) / 2;
        shortened.FreeFlowMinutes /= 2;
        newNetwork.ValidateMapping(citizen.Journey, saved.Route); // The shortened claim alone is geometrically valid.
        Assert.Throws<ArgumentException>(() => newNetwork.ValidateRemainingMapping(citizen.Journey, saved.Route, progress));
    }

    [Fact]
    public void OrdinaryCollinearCurbConnectorsAndLegacyEmptyFragmentsAreNotPromotedToNativeRoads()
    {
        TrafficNetwork network = Straight();
        Route route = Journey(Leg(P(-1), P(0), 0.1), Leg(P(0), P(10), 1, Tag(0, 1)), Leg(P(10), P(11), 0.1));
        Assert.True(network.TryMap(route, out var native));
        Assert.Equal(0.1, Assert.Single(native).StartNominalMinutes);
        Assert.Equal(1.1, native[0].EndNominalMinutes);
        network.ValidateRemainingMapping(route, native, 0);
        Route legacy = Journey(Leg(P(0), P(5), 0.5), Leg(P(5), P(10), 0.5));
        Assert.False(network.TryMap(legacy, out var absent));
        Assert.Empty(absent);
    }


    [Fact]
    public void ARepresentableTinyNativeFragmentRetainsDirectionalProvenance()
    {
        TrafficNetwork network = Straight();
        Route route = Journey(Leg(P(10 - 1e-7), P(10), 1e-8, Fragment(0, 1)));
        Assert.True(network.TryMap(route, out var mapped));
        network.ValidateRemainingMapping(route, mapped, 0);
        route.Legs[0].To = route.Legs[0].From;
        Assert.False(network.TryMap(route, out _));
    }

}
