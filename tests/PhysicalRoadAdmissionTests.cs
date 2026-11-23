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

    private static FenceDoor VehicleGate(string name, Vector3 position)
    {
        var door = new FenceDoor { name = name, acceptsVehicles = true };
        door.transform.position = position;
        return door;
    }

    [Fact]
    public void VehicleGateWithColliderGeometryUsesExpandedColliderBoundsNotFallbackBox()
    {
        var building = new CityBuildingController { name = "WarehouseBuilding" };
        var buildingBox = new BoxCollider { center = Vector3.zero, size = new Vector3(30, 12, 30) };
        buildingBox.transform.position = new Vector3(100, 6, 0);
        buildingBox.transform.SetParent(building.transform);

        var door = VehicleGate("WarehouseGateCollider", new Vector3(100, 1.5f, 0));
        door.transform.SetParent(building.transform);
        var collider = new BoxCollider { center = Vector3.zero, size = new Vector3(6, 3, 2) };
        collider.transform.position = new Vector3(100, 1.5f, 0);
        collider.transform.SetParent(door.transform);
        var largerRenderer = new Renderer { bounds = new Bounds(new Vector3(100, 2, 0), new Vector3(24, 10, 24)) };
        largerRenderer.transform.SetParent(door.transform);

        var logs = new List<string>();
        var access = new PublicRoadAccess(new[] { door }, logs.Add);

        Assert.Equal(0, access.FallbackGateCount);
        Assert.Empty(access.FallbackGateDoors);
        Assert.Empty(logs);
        Assert.False(access.ClearSegment(new Position(90, 1.5, 0), new Position(110, 1.5, 0)));
        Assert.False(access.ClearSegment(new Position(96.5, 1.5, -5), new Position(96.5, 1.5, 5)));
        Assert.False(access.ClearSegment(new Position(103.5, 1.5, -5), new Position(103.5, 1.5, 5)));
        Assert.True(access.ClearSegment(new Position(104, 1.5, -5), new Position(104, 1.5, 5)));
        Assert.True(access.ClearSegment(new Position(110, 1.5, -10), new Position(110, 1.5, 10)));
        Assert.True(access.NearGate(new Position(104.4, 1.5, 0), 1));
        Assert.False(access.NearGate(new Position(105, 1.5, 0), 1));
    }

    [Fact]
    public void VehicleGateWithoutCollidersUsesRendererBoundsAndUnionsSwingingLeafOnRefresh()
    {
        var door = VehicleGate("RendererOnlyGate", new Vector3(100, 2, 0));
        var degenerate = new Collider { bounds = new Bounds(new Vector3(100, 2, 0), Vector3.zero) };
        degenerate.transform.SetParent(door.transform);
        var invalid = new Collider { bounds = new Bounds(new Vector3(float.NaN, 0, 0), new Vector3(4, 4, 4)) };
        invalid.transform.SetParent(door.transform);

        var frame = new Renderer { bounds = new Bounds(new Vector3(100, 2, 0), new Vector3(2, 4, 2)) };
        frame.transform.SetParent(door.transform);
        var leafTransform = new Transform { position = new Vector3(102, 2, 0) };
        leafTransform.SetParent(door.transform);
        var leafRenderer = new Renderer { bounds = new Bounds(new Vector3(102, 2, 0), new Vector3(2, 4, 2)) };
        leafRenderer.transform = leafTransform;
        door.doors = new[] { leafTransform };

        var logs = new List<string>();
        var access = new PublicRoadAccess(new[] { door }, logs.Add);

        Assert.Equal(0, access.FallbackGateCount);
        Assert.Empty(access.FallbackGateDoors);
        Assert.Empty(logs);
        Assert.False(access.ClearSegment(new Position(98.5, 2, -5), new Position(98.5, 2, 5)));
        Assert.False(access.ClearSegment(new Position(103.5, 2, -5), new Position(103.5, 2, 5)));
        Assert.True(access.ClearSegment(new Position(104.5, 2, -5), new Position(104.5, 2, 5)));
        Assert.True(access.ClearSegment(new Position(112, 2, -10), new Position(112, 2, 10)));

        leafRenderer.bounds = new Bounds(new Vector3(100, 2, 4), new Vector3(2, 4, 2));
        var refreshed = access.Refreshed(new[] { door }, out string change);

        Assert.NotNull(refreshed);
        Assert.Contains("RendererOnlyGate", change, StringComparison.Ordinal);
        Assert.Equal(0, refreshed!.FallbackGateCount);
        Assert.Empty(logs);
        Assert.False(refreshed.ClearSegment(new Position(103.5, 2, -5), new Position(103.5, 2, 5)));
        Assert.False(refreshed.ClearSegment(new Position(95, 2, 5.5), new Position(105, 2, 5.5)));
        Assert.True(refreshed.ClearSegment(new Position(112, 2, -10), new Position(112, 2, 10)));
    }

    [Fact]
    public void VehicleGateWithoutCollidersOrRenderersUsesParentBuildingFootprintNotFallbackBox()
    {
        var building = new CityBuildingController { name = "DepotBuilding" };
        var solidFootprint = new BoxCollider { center = Vector3.zero, size = new Vector3(10, 8, 6), enabled = true, isTrigger = false };
        solidFootprint.transform.position = new Vector3(100, 4, 0);
        solidFootprint.transform.SetParent(building.transform);
        var ignoredTrigger = new BoxCollider { center = Vector3.zero, size = new Vector3(60, 20, 60), enabled = true, isTrigger = true };
        ignoredTrigger.transform.position = new Vector3(100, 4, 0);
        ignoredTrigger.transform.SetParent(building.transform);
        var ignoredDisabled = new BoxCollider { center = Vector3.zero, size = new Vector3(60, 20, 60), enabled = false, isTrigger = false };
        ignoredDisabled.transform.position = new Vector3(100, 4, 0);
        ignoredDisabled.transform.SetParent(building.transform);

        var door = VehicleGate("DepotGateFootprint", new Vector3(100, 1, 3));
        door.transform.SetParent(building.transform);

        var logs = new List<string>();
        var access = new PublicRoadAccess(new[] { door }, logs.Add);

        Assert.Equal(0, access.FallbackGateCount);
        Assert.Empty(access.FallbackGateDoors);
        Assert.Empty(logs);
        Assert.False(access.ClearSegment(new Position(94.5, 1, -10), new Position(94.5, 1, 10)));
        Assert.False(access.ClearSegment(new Position(105.5, 1, -10), new Position(105.5, 1, 10)));
        Assert.True(access.ClearSegment(new Position(106.5, 1, -10), new Position(106.5, 1, 10)));
        Assert.True(access.ClearSegment(new Position(112, 1, -10), new Position(112, 1, 10)));

        var visualBuilding = new CityBuildingController { name = "VisualOnlyBuilding" };
        var buildingRenderer = new Renderer { enabled = true, bounds = new Bounds(new Vector3(200, 3, 0), new Vector3(8, 6, 4)) };
        buildingRenderer.transform.SetParent(visualBuilding.transform);
        var visualDoor = VehicleGate("VisualDepotGate", new Vector3(200, 1, 2));
        visualDoor.transform.SetParent(visualBuilding.transform);

        var visualAccess = new PublicRoadAccess(new[] { visualDoor }, logs.Add);
        Assert.Equal(0, visualAccess.FallbackGateCount);
        Assert.Empty(logs);
        Assert.False(visualAccess.ClearSegment(new Position(195.5, 1, -5), new Position(195.5, 1, 5)));
        Assert.False(visualAccess.ClearSegment(new Position(204.5, 1, -5), new Position(204.5, 1, 5)));
        Assert.True(visualAccess.ClearSegment(new Position(210, 1, -5), new Position(210, 1, 5)));
    }

    [Fact]
    public void VehicleGateWithoutShapeIsCountedLoggedOncePerBuildOrRefreshAndKeepsConservativeBox()
    {
        var orphanGate = VehicleGate("OrphanVehicleGate", new Vector3(100, 0, 0));
        var emptyBuilding = new CityBuildingController { name = "EmptyBuilding" };
        var triggerOnly = new BoxCollider { center = Vector3.zero, size = new Vector3(12, 6, 12), enabled = true, isTrigger = true };
        triggerOnly.transform.position = new Vector3(300, 3, 0);
        triggerOnly.transform.SetParent(emptyBuilding.transform);
        var hamptonsGate = new HamptonsPrivateFenceDoor { name = "HamptonsNoShapeGate", acceptsVehicles = false, ForSale = true };
        hamptonsGate.transform.position = new Vector3(300, 0, 0);
        hamptonsGate.transform.SetParent(emptyBuilding.transform);

        var pedestrianOnly = new FenceDoor { name = "PedestrianGate", acceptsVehicles = false };
        pedestrianOnly.transform.position = new Vector3(500, 0, 0);
        var forSaleGate = new FenceDoor { name = "ForSaleGate", acceptsVehicles = true, ForSale = true };
        forSaleGate.transform.position = new Vector3(600, 0, 0);

        var doors = new FenceDoor[] { null!, orphanGate, hamptonsGate, pedestrianOnly, forSaleGate };
        var logs = new List<string>();
        var access = new PublicRoadAccess(doors, logs.Add);

        Assert.Equal(2, access.FallbackGateCount);
        Assert.Equal(new[] { "OrphanVehicleGate", "HamptonsNoShapeGate" }, access.FallbackGateDoors);
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, line => line.Contains("OrphanVehicleGate", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("HamptonsNoShapeGate", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, line => line.Contains("PedestrianGate", StringComparison.Ordinal) || line.Contains("ForSaleGate", StringComparison.Ordinal));

        Assert.False(access.ClearSegment(new Position(70, 0, 0), new Position(130, 0, 0)));
        Assert.False(access.ClearSegment(new Position(115, 0, -5), new Position(115, 0, 5)));
        Assert.False(access.ClearSegment(new Position(290, 0, -5), new Position(290, 0, 5)));
        Assert.True(access.ClearSegment(new Position(150, 0, 0), new Position(250, 0, 0)));
        Assert.True(access.ClearSegment(new Position(495, 0, 0), new Position(505, 0, 0)));
        Assert.True(access.ClearSegment(new Position(595, 0, 0), new Position(605, 0, 0)));
        Assert.Equal(2, logs.Count);

        var unchanged = access.Refreshed(doors, out string unchangedReason);
        Assert.Null(unchanged);
        Assert.Equal("", unchangedReason);
        Assert.Equal(2, access.FallbackGateCount);
        Assert.Equal(4, logs.Count);

        var addedCollider = new BoxCollider { center = Vector3.zero, size = new Vector3(4, 3, 2) };
        addedCollider.transform.position = new Vector3(100, 1.5f, 0);
        addedCollider.transform.SetParent(orphanGate.transform);
        var updated = access.Refreshed(doors, out string updatedReason);

        Assert.NotNull(updated);
        Assert.Contains("OrphanVehicleGate", updatedReason, StringComparison.Ordinal);
        Assert.Equal(1, updated!.FallbackGateCount);
        Assert.Equal(new[] { "HamptonsNoShapeGate" }, updated.FallbackGateDoors);
        Assert.Equal(5, logs.Count);
        Assert.Contains("HamptonsNoShapeGate", logs[4], StringComparison.Ordinal);
        Assert.True(updated.ClearSegment(new Position(115, 0, -5), new Position(115, 0, 5)));

        var defaultLogGate = VehicleGate("GateDefaultLogCheck_331", new Vector3(800, 0, 0));
        var defaultAccess = new PublicRoadAccess(new[] { defaultLogGate });
        Assert.Equal(1, defaultAccess.FallbackGateCount);
        Assert.Contains(Debug.Warnings(), line => line.Contains("GateDefaultLogCheck_331", StringComparison.Ordinal));
    }
}
