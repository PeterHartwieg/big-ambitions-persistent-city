using System;
using System.Collections.Generic;
using System.Linq;
using Buildings;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalRoadAdmissionTests
{
    private static RoadBoundary Enter(double distance, int junction = 0) => new(distance, true, false, junction);
    private static RoadBoundary Exit(double distance, int junction = 0) => new(distance, false, true, junction);

    [Theory]
    [InlineData(0, true)]
    [InlineData(14.99, true)]
    [InlineData(15, true)]
    [InlineData(15.01, false)]
    [InlineData(20, false)]
    [InlineData(30, false)]
    [InlineData(40, false)]
    [InlineData(44.99, false)]
    [InlineData(45, true)]
    public void WholeBodyMustRemainOutsideNativeTraversal(double progress, bool allowed)
    {
        Assert.Equal(allowed, PhysicalRoadAdmission.Allows(new[] { Enter(20), Exit(40) }, progress, 4));
    }

    [Fact]
    public void LaterJunctionStillRequiresItsOwnStopLineClearance()
    {
        var boundaries = new[] { Enter(20), Exit(40), Enter(60, 1), Exit(80, 1) };
        Assert.True(PhysicalRoadAdmission.Allows(boundaries, 50, 4));
        Assert.False(PhysicalRoadAdmission.Allows(boundaries, 56, 4));
        Assert.False(PhysicalRoadAdmission.Allows(boundaries, 70, 4));
        Assert.True(PhysicalRoadAdmission.Allows(boundaries, 85, 4));
    }

    [Fact]
    public void UnknownAndIncompleteOrAmbiguousBoundaryHistoryCannotAuthorizeAcquisition()
    {
        foreach (var boundaries in new[]
        {
            System.Array.Empty<RoadBoundary>(), new[] { Enter(20) },
            new[] { Enter(20), Exit(40, 1) }, new[] { Enter(20), Enter(30), Exit(40) },
            new[] { Enter(20), Exit(20) }, new[] { Enter(20), new RoadBoundary(40, false, true, -1) }
        }) Assert.False(PhysicalRoadAdmission.Allows(boundaries, 0, 4));
        Assert.False(PhysicalRoadAdmission.Allows(new[] { Enter(20), Exit(40) }, double.NaN, 4));
        Assert.False(PhysicalRoadAdmission.Allows(new[] { Enter(20), Exit(40) }, 0, double.NaN));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 4.999, false)]
    [InlineData(0, 5, true)]
    [InlineData(0, 100, true)]
    [InlineData(40, 0, false)]
    [InlineData(40, 39.999, false)]
    [InlineData(40, 40, false)]
    [InlineData(40, 44.999, false)]
    [InlineData(40, 45, true)]
    [InlineData(40, 100, true)]
    public void LeadingExitProvesOutsideOnlyAfterTheWholeBodyAndMarginHaveCleared(double exit, double progress, bool allowed)
    {
        Assert.Equal(allowed, PhysicalRoadAdmission.Allows(new[] { Exit(exit) }, progress, 4));
    }

    [Fact]
    public void LeadingExitDoesNotBypassSubsequentStopLineTraversalOrRearClearance()
    {
        var boundaries = new[] { Exit(0), Enter(20, 1), Exit(40, 1) };
        Assert.False(PhysicalRoadAdmission.Allows(boundaries, 4.999, 4));
        Assert.True(PhysicalRoadAdmission.Allows(boundaries, 5, 4));
        Assert.True(PhysicalRoadAdmission.Allows(boundaries, 15, 4));
        Assert.False(PhysicalRoadAdmission.Allows(boundaries, 15.001, 4));
        Assert.False(PhysicalRoadAdmission.Allows(boundaries, 30, 4));
        Assert.False(PhysicalRoadAdmission.Allows(boundaries, 44.999, 4));
        Assert.True(PhysicalRoadAdmission.Allows(boundaries, 45, 4));
        Assert.False(PhysicalRoadAdmission.Allows(boundaries, 45, 4, margin: 2));
    }

    [Fact]
    public void LeadingExitCannotRepairLaterMissingMismatchedNestedOrFinalBoundaries()
    {
        foreach (var boundaries in new[]
        {
            new[] { Exit(0), Exit(10) },
            new[] { Exit(0), Enter(20, 1), Exit(40, 2) },
            new[] { Exit(0), Enter(20, 1), Exit(40, 1), Exit(60, 2) },
            new[] { Exit(0), Enter(20, 1), Enter(30, 2), Exit(40, 2) },
            new[] { Exit(0), Enter(20, 1) },
            new[] { Exit(0), Enter(20, 1), Exit(20, 1) },
            new[] { new RoadBoundary(0, false, true, -1), Enter(20, 1), Exit(40, 1) },
            new[] { new RoadBoundary(0, true, true, 0), Enter(20, 1), Exit(40, 1) }
        })
        {
            Assert.False(PhysicalRoadAdmission.Allows(boundaries, 10, 4));
            Assert.False(PhysicalRoadAdmission.Allows(boundaries, 100, 4));
        }
    }

    [Fact]
    public void CapturedC106NativeRouteCanBindBetweenJunctionsWithoutAuthorizingTheirTraversal()
    {
        var boundaries = new[] {
            Exit(0), Enter(150.73696652311065, 1), Exit(187.2369111821057, 1),
            Enter(297.7223215040971, 2), Exit(320.7131921657034, 2),
            Enter(379.5177016674547, 3), Exit(408.0182542418408, 3),
            Enter(466.73027561648456, 4), Exit(495.3362832318024, 4),
            Enter(554.1934841686198, 5), Exit(582.5191573148148, 5)
        };
        const double length = 4.352025985717773;
        Assert.True(PhysicalRoadAdmission.Allows(boundaries, 424.1598302145013, length));
        Assert.False(PhysicalRoadAdmission.Allows(boundaries, 0, length));
        foreach (var pair in new[] { (150.73696652311065, 187.2369111821057), (297.7223215040971, 320.7131921657034),
            (379.5177016674547, 408.0182542418408), (466.73027561648456, 495.3362832318024), (554.1934841686198, 582.5191573148148) })
        {
            Assert.False(PhysicalRoadAdmission.Allows(boundaries, pair.Item1 - 1, length));
            Assert.False(PhysicalRoadAdmission.Allows(boundaries, (pair.Item1 + pair.Item2) / 2, length));
            Assert.False(PhysicalRoadAdmission.Allows(boundaries, pair.Item2 + length, length));
            Assert.True(PhysicalRoadAdmission.Allows(boundaries, pair.Item2 + length + 1, length));
        }
    }

    [Fact]
    public void VehicleGateWithColliderGeometryUsesExpandedColliderBoundsNotRendererFootprintOrFallback()
    {
        var gate = VehicleGate("collider gate");
        gate.Colliders = new Collider[]
        {
            new BoxCollider { transform = { position = gate.transform.position }, center = new Vector3(1, 0, 0), size = new Vector3(4, 2, 2) },
            new Collider { bounds = new Bounds(new Vector3(105, 3, 200), new Vector3(2, 2, 2)) }
        };
        gate.Renderers = new[] { new Renderer { bounds = new Bounds(gate.transform.position, new Vector3(30, 12, 30)) } };
        gate.transform.parent = new Transform { Component = BuildingWithBounds(gate.transform.position, new Vector3(60, 20, 60)) };
        var logs = new List<string>();

        var access = new PublicRoadAccess(new[] { gate }, logs.Add);

        Assert.Equal(0, access.FallbackGateCount);
        Assert.Empty(logs);
        Assert.False(ClearAcrossGate(access, 98.75, 3));
        Assert.True(ClearAcrossGate(access, 98.25, 3));
        Assert.False(ClearAcrossGate(access, 106.25, 4.75));
        Assert.True(ClearAcrossGate(access, 106.75, 4.75));
        Assert.True(ClearAcrossGate(access, 105, 5.25));
        Assert.True(ClearAcrossGate(access, 115, 3));
        Assert.True(access.NearGate(new Position(101, 3, 201.25), 0));
        Assert.False(access.NearGate(new Position(101, 3, 201.75), 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VehicleGateWithoutUsableCollidersUsesExpandedRendererBoundsNotFootprintOrFallback(bool emptyCollider)
    {
        var gate = VehicleGate("renderer gate");
        if (emptyCollider) gate.Colliders = new[] { new Collider(), new Collider { bounds = new Bounds(new Vector3(float.NaN, 3, 200), new Vector3(4, 4, 4)) } };
        gate.Renderers = new[]
        {
            new Renderer { bounds = new Bounds(new Vector3(101, 3, 200), new Vector3(2, 2, 2)) },
            new Renderer { bounds = new Bounds(new Vector3(104, 3, 200), new Vector3(2, 2, 2)) },
            new Renderer { bounds = new Bounds(new Vector3(float.NaN, 3, 200), new Vector3(4, 4, 4)) }
        };
        gate.transform.parent = new Transform { Component = BuildingWithBounds(gate.transform.position, new Vector3(30, 12, 30)) };
        var logs = new List<string>();

        var access = new PublicRoadAccess(new[] { gate }, logs.Add);

        Assert.Equal(0, access.FallbackGateCount);
        Assert.Empty(logs);
        Assert.True(ClearAcrossGate(access, 99.25, 3));
        Assert.False(ClearAcrossGate(access, 99.75, 3));
        Assert.False(ClearAcrossGate(access, 105.25, 4.75));
        Assert.True(ClearAcrossGate(access, 105.75, 3));
        Assert.True(ClearAcrossGate(access, 102, 5.25));
        Assert.True(ClearAcrossGate(access, 112, 3));
    }

    [Fact]
    public void VehicleGateWithoutCollidersUsesRendererBoundsAndUnionsSwingingLeafOnRefresh()
    {
        var gate = VehicleGate("renderer leaf gate", new Vector3(100, 2, 0));
        var leaf = new Transform { parent = gate.transform };
        gate.doors = new[] { leaf };
        var frame = new Renderer { bounds = new Bounds(new Vector3(100, 2, 0), new Vector3(2, 4, 2)) };
        var leafRenderer = new Renderer { transform = { parent = leaf }, bounds = new Bounds(new Vector3(102, 2, 0), new Vector3(2, 4, 2)) };
        gate.Renderers = new[] { frame, leafRenderer };
        var logs = new List<string>();

        var access = new PublicRoadAccess(new[] { gate }, logs.Add);

        Assert.Equal(0, access.FallbackGateCount);
        Assert.False(access.ClearSegment(new Position(98.75, 2, -5), new Position(98.75, 2, 5)));
        Assert.False(access.ClearSegment(new Position(103.25, 2, -5), new Position(103.25, 2, 5)));
        Assert.True(access.ClearSegment(new Position(103.75, 2, -5), new Position(103.75, 2, 5)));
        Assert.True(access.ClearSegment(new Position(95, 2, 5.25), new Position(105, 2, 5.25)));

        leafRenderer.bounds = new Bounds(new Vector3(100, 2, 4), new Vector3(2, 4, 2));
        var refreshed = access.Refreshed(new[] { gate }, out string change);

        Assert.NotNull(refreshed);
        Assert.Contains("renderer leaf gate", change, StringComparison.Ordinal);
        Assert.Equal(0, refreshed!.FallbackGateCount);
        Assert.Empty(logs);
        Assert.False(refreshed.ClearSegment(new Position(103.25, 2, -1), new Position(103.25, 2, 1)));
        Assert.False(refreshed.ClearSegment(new Position(95, 2, 5.25), new Position(105, 2, 5.25)));
        Assert.True(refreshed.ClearSegment(new Position(103.75, 2, -10), new Position(103.75, 2, 10)));
        Assert.True(refreshed.ClearSegment(new Position(95, 2, 5.75), new Position(105, 2, 5.75)));
    }

    [Fact]
    public void RendererGateWhoseOnlyRendererIsTheLeafKeepsTheClosedLeafAreaWhenItOpens()
    {
        var gate = VehicleGate("leaf only gate", new Vector3(100, 2, 0));
        var leaf = new Transform { parent = gate.transform };
        gate.doors = new[] { leaf };
        var leafRenderer = new Renderer { transform = { parent = leaf }, bounds = new Bounds(new Vector3(102, 2, 0), new Vector3(2, 4, 2)) };
        gate.Renderers = new[] { leafRenderer };
        var access = new PublicRoadAccess(new[] { gate }, _ => { });
        Assert.False(access.ClearSegment(new Position(102, 2, -5), new Position(102, 2, 5)));

        leafRenderer.bounds = new Bounds(new Vector3(100, 2, 4), new Vector3(2, 4, 2));
        var refreshed = access.Refreshed(new[] { gate }, out _);

        Assert.NotNull(refreshed);
        Assert.False(refreshed!.ClearSegment(new Position(102, 2, -5), new Position(102, 2, 5)));
        Assert.False(refreshed.ClearSegment(new Position(95, 2, 4), new Position(105, 2, 4)));

        gate.transform.position = new Vector3(300, 2, 0);
        leafRenderer.bounds = new Bounds(new Vector3(302, 2, 0), new Vector3(2, 4, 2));
        var moved = refreshed.Refreshed(new[] { gate }, out _);

        Assert.NotNull(moved);
        Assert.True(moved!.ClearSegment(new Position(102, 2, -5), new Position(102, 2, 5)));
        Assert.False(moved.ClearSegment(new Position(302, 2, -5), new Position(302, 2, 5)));
    }

    [Fact]
    public void FootprintGateKeepsItsSiblingLeafAreaWhenTheLeafSwings()
    {
        var gate = VehicleGate("footprint leaf gate");
        var building = BuildingWithBounds(new Vector3(100, 3, 200), new Vector3(12, 6, 8));
        var buildingRoot = new Transform { Component = building };
        gate.transform.parent = buildingRoot;
        var leaf = new Transform { parent = buildingRoot };
        gate.doors = new[] { leaf };
        var leafCollider = new Collider { transform = { parent = leaf }, bounds = new Bounds(new Vector3(110, 3, 200), new Vector3(4, 4, 2)) };
        building.Colliders = building.Colliders.Append(leafCollider).ToArray();
        var access = new PublicRoadAccess(new[] { gate }, _ => { });
        Assert.False(ClearAcrossGate(access, 111, 3));
        Assert.True(ClearAcrossGate(access, 113, 3));

        leafCollider.bounds = new Bounds(new Vector3(100, 3, 206), new Vector3(2, 4, 4));
        var refreshed = access.Refreshed(new[] { gate }, out _);

        Assert.NotNull(refreshed);
        Assert.False(ClearAcrossGate(refreshed!, 111, 3));
        Assert.True(ClearAcrossGate(refreshed, 113, 3));
        Assert.False(refreshed.ClearSegment(new Position(95, 3, 207.5), new Position(105, 3, 207.5)));
    }

    [Fact]
    public void GateThatGainsCollidersDoesNotKeepItsOldRendererLeafUnion()
    {
        var gate = VehicleGate("renderer then collider", new Vector3(100, 2, 0));
        var leaf = new Transform { parent = gate.transform };
        gate.doors = new[] { leaf };
        gate.Renderers = new[]
        {
            new Renderer { bounds = new Bounds(new Vector3(100, 2, 0), new Vector3(2, 4, 2)) },
            new Renderer { transform = { parent = leaf }, bounds = new Bounds(new Vector3(110, 2, 0), new Vector3(2, 4, 2)) }
        };
        var access = new PublicRoadAccess(new[] { gate }, _ => { });
        Assert.False(access.ClearSegment(new Position(110, 2, -5), new Position(110, 2, 5)));

        gate.Colliders = new[] { new Collider { bounds = new Bounds(new Vector3(100, 2, 0), new Vector3(2, 4, 2)) } };
        var refreshed = access.Refreshed(new[] { gate }, out _);

        Assert.NotNull(refreshed);
        Assert.False(refreshed!.ClearSegment(new Position(101.25, 2, -5), new Position(101.25, 2, 5)));
        Assert.True(refreshed.ClearSegment(new Position(101.75, 2, -5), new Position(101.75, 2, 5)));
        Assert.True(refreshed.ClearSegment(new Position(110, 2, -5), new Position(110, 2, 5)));
    }

    [Fact]
    public void VehicleGateWithoutCollidersOrRenderersUsesExpandedParentBuildingFootprintNotFallback()
    {
        var gate = VehicleGate("footprint gate");
        gate.Renderers = new[] { new Renderer() };
        var building = BuildingWithBounds(new Vector3(100, 3, 200), new Vector3(12, 6, 8));
        building.Colliders = building.Colliders.Concat(new[]
        {
            new Collider { isTrigger = true, bounds = new Bounds(new Vector3(100, 3, 200), new Vector3(60, 20, 60)) },
            new Collider { enabled = false, bounds = new Bounds(new Vector3(100, 3, 200), new Vector3(60, 20, 60)) },
            new Collider { bounds = new Bounds(new Vector3(0, 0, 0), Vector3.zero) },
            new Collider { bounds = new Bounds(new Vector3(300, 0, 300), Vector3.zero) }
        }).ToArray();
        gate.transform.parent = new Transform { parent = new Transform { Component = building } };
        var logs = new List<string>();

        var access = new PublicRoadAccess(new[] { gate }, logs.Add);

        Assert.Equal(0, access.FallbackGateCount);
        Assert.Empty(logs);
        Assert.True(ClearAcrossGate(access, 93.25, 3));
        Assert.False(ClearAcrossGate(access, 93.75, 3));
        Assert.False(ClearAcrossGate(access, 106.25, 6.75));
        Assert.True(ClearAcrossGate(access, 106.75, 3));
        Assert.True(ClearAcrossGate(access, 100, 7.25));
        Assert.True(ClearAcrossGate(access, 150, 3));
    }

    [Fact]
    public void ShapelessGatesAreCountedLoggedOnlyWhenTheTallyChangesAndKeepTheConservativeBox()
    {
        var vehicle = VehicleGate("shapeless vehicle gate");
        var privateGate = new HamptonsPrivateFenceDoor { name = "shapeless private gate", ForSale = true, transform = { position = new Vector3(180, 3, 200) } };
        var pedestrian = new FenceDoor { name = "pedestrian gate", transform = { position = new Vector3(300, 3, 200) } };
        var forSale = new FenceDoor { name = "for sale gate", acceptsVehicles = true, ForSale = true, transform = { position = new Vector3(400, 3, 200) } };
        FenceDoor[] doors = { null!, vehicle, privateGate, pedestrian, forSale };
        var logs = new List<string>();

        var access = new PublicRoadAccess(doors, logs.Add);

        Assert.Equal(2, access.FallbackGateCount);
        Assert.Equal(new[] { "shapeless private gate", "shapeless vehicle gate" }, access.FallbackGateDoors);
        var line = Assert.Single(logs);
        Assert.Contains("2 using the 40x20x40 m fallback", line, StringComparison.Ordinal);
        Assert.Contains("shapeless vehicle gate", line, StringComparison.Ordinal);
        Assert.Contains("shapeless private gate", line, StringComparison.Ordinal);
        Assert.False(ClearAcrossGate(access, 80.25, 3));
        Assert.False(ClearAcrossGate(access, 119.75, 12.75));
        Assert.True(ClearAcrossGate(access, 120.25, 3));
        Assert.True(ClearAcrossGate(access, 100, 13.25));
        Assert.False(ClearAcrossGate(access, 180, 3));
        Assert.True(ClearAcrossGate(access, 300, 3));
        Assert.True(ClearAcrossGate(access, 400, 3));

        Assert.Null(access.Refreshed(doors, out string unchanged));
        Assert.Equal("", unchanged);
        Assert.Equal(2, access.FallbackGateCount);
        Assert.Single(logs);

        vehicle.Colliders = new[] { new Collider { bounds = new Bounds(vehicle.transform.position, new Vector3(4, 2, 2)) } };
        var updated = access.Refreshed(doors, out string change);

        Assert.NotNull(updated);
        Assert.Contains("shapeless vehicle gate", change, StringComparison.Ordinal);
        Assert.Equal(1, updated!.FallbackGateCount);
        Assert.Equal(new[] { "shapeless private gate" }, updated.FallbackGateDoors);
        Assert.Equal(2, logs.Count);
        Assert.Contains("1 using the 40x20x40 m fallback: shapeless private gate", logs[1], StringComparison.Ordinal);
        Assert.DoesNotContain("shapeless vehicle gate", logs[1], StringComparison.Ordinal);
        Assert.True(updated.ClearSegment(new Position(115, 3, 175), new Position(115, 3, 225)));

        Assert.Null(updated.Refreshed(doors, out _));
        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public void RefreshUpdatesFallbackCountWhenNewGeometryHasTheSameBounds()
    {
        var gate = VehicleGate("geometry appears");
        var logs = new List<string>();
        var access = new PublicRoadAccess(new[] { gate }, logs.Add);
        Assert.Equal(1, access.FallbackGateCount);

        gate.Renderers = new[] { new Renderer { bounds = new Bounds(gate.transform.position, new Vector3(39, 18, 39)) } };

        Assert.Null(access.Refreshed(new[] { gate }, out _));
        Assert.Equal(0, access.FallbackGateCount);
        Assert.Empty(access.FallbackGateDoors);
        Assert.Equal(2, logs.Count);
        Assert.Contains("0 using the 40x20x40 m fallback.", logs[1], StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultFallbackLogGoesToTheUnityWarningLog()
    {
        var gate = VehicleGate(nameof(DefaultFallbackLogGoesToTheUnityWarningLog));

        var access = new PublicRoadAccess(new[] { gate });

        Assert.Equal(1, access.FallbackGateCount);
        Assert.Single(Debug.Warnings(), line => line.StartsWith("[Persistent City]", StringComparison.Ordinal)
            && line.Contains(gate.name, StringComparison.Ordinal));
    }

    private static FenceDoor VehicleGate(string name) => VehicleGate(name, new Vector3(100, 3, 200));

    private static FenceDoor VehicleGate(string name, Vector3 position) =>
        new() { name = name, acceptsVehicles = true, transform = { position = position } };

    private static CityBuildingController BuildingWithBounds(Vector3 centre, Vector3 size) =>
        new() { Colliders = new[] { new Collider { bounds = new Bounds(centre, size) } } };

    private static bool ClearAcrossGate(PublicRoadAccess access, double x, double y) =>
        access.ClearSegment(new Position(x, y, 175), new Position(x, y, 225));
}
