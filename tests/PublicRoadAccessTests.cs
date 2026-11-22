using System;
using System.Linq;
using Buildings;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PublicRoadAccessTests
{
    [Fact]
    public void VehicleGateKeepsCombinedColliderBoundsAndPaddingAheadOfRenderers()
    {
        var gate = VehicleGate("collider gate");
        gate.Colliders = new Collider[]
        {
            new BoxCollider
            {
                transform = { position = gate.transform.position },
                center = new Vector3(1, 0, 0),
                size = new Vector3(4, 2, 2)
            },
            new Collider { bounds = new Bounds(new Vector3(105, 3, 200), new Vector3(2, 2, 2)) }
        };
        gate.Renderers = new[] { new Renderer { bounds = new Bounds(gate.transform.position, new Vector3(30, 12, 30)) } };

        var access = new PublicRoadAccess(new[] { gate });

        Assert.Equal(0, access.FallbackGateCount);
        Assert.False(ClearAcrossGate(access, 98.75, 3));
        Assert.False(ClearAcrossGate(access, 106.25, 4.75));
        Assert.True(ClearAcrossGate(access, 106.75, 4.75));
        Assert.True(ClearAcrossGate(access, 105, 5.25));
        Assert.True(access.NearGate(new Position(101, 3, 201.25), 0));
        Assert.False(access.NearGate(new Position(101, 3, 201.75), 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VehicleGateWithoutUsableCollidersUsesCombinedRendererBounds(bool emptyCollider)
    {
        var gate = VehicleGate("renderer gate");
        if (emptyCollider) gate.Colliders = new[] { new Collider() };
        gate.Renderers = new[]
        {
            new Renderer { bounds = new Bounds(new Vector3(101, 3, 200), new Vector3(2, 2, 2)) },
            new Renderer { bounds = new Bounds(new Vector3(104, 3, 200), new Vector3(2, 2, 2)) },
            new Renderer { bounds = new Bounds(new Vector3(float.NaN, 3, 200), new Vector3(4, 4, 4)) }
        };
        gate.transform.parent = new Transform { Component = BuildingWithBounds(gate, new Vector3(30, 12, 30)) };

        var access = new PublicRoadAccess(new[] { gate });

        Assert.Equal(0, access.FallbackGateCount);
        Assert.False(ClearAcrossGate(access, 100.5, 3));
        Assert.False(ClearAcrossGate(access, 104.5, 3));
        Assert.True(ClearAcrossGate(access, 106, 3));
    }

    [Fact]
    public void VehicleGateWithoutRendererOrColliderShapeUsesItsParentBuildingFootprint()
    {
        var gate = VehicleGate("building gate");
        gate.Renderers = new[] { new Renderer() };
        gate.transform.parent = new Transform { Component = BuildingWithBounds(gate, new Vector3(12, 6, 8)) };

        var access = new PublicRoadAccess(new[] { gate });

        Assert.Equal(0, access.FallbackGateCount);
        Assert.False(ClearAcrossGate(access, 105, 3));
        Assert.True(ClearAcrossGate(access, 107, 3));
    }

    [Fact]
    public void ShapelessVehicleAndPrivateGatesLogEachFallbackOncePerBuildOrRefresh()
    {
        var vehicle = VehicleGate(nameof(ShapelessVehicleAndPrivateGatesLogEachFallbackOncePerBuildOrRefresh) + " vehicle");
        var privateGate = new HamptonsPrivateFenceDoor { name = vehicle.name + " private", transform = { position = new Vector3(180, 3, 200) } };
        FenceDoor[] doors = { vehicle, privateGate, new FenceDoor() };
        int firstLog = Debug.Warnings().Count;

        var access = new PublicRoadAccess(doors);

        Assert.Equal(2, access.FallbackGateCount);
        Assert.False(ClearAcrossGate(access, 119, 3));
        Assert.True(ClearAcrossGate(access, 121, 3));
        Assert.False(ClearAcrossGate(access, 180, 3));
        Assert.True(ClearAcrossGate(access, 220, 3));
        AssertFallbackWarnings(firstLog, vehicle, privateGate);

        firstLog = Debug.Warnings().Count;
        Assert.Null(access.Refreshed(doors, out _));
        Assert.Equal(2, access.FallbackGateCount);
        AssertFallbackWarnings(firstLog, vehicle, privateGate);
    }

    [Fact]
    public void RefreshUpdatesFallbackCountWhenNewGeometryHasTheSameBounds()
    {
        var gate = VehicleGate("geometry appears");
        var access = new PublicRoadAccess(new[] { gate });
        Assert.Equal(1, access.FallbackGateCount);

        gate.Renderers = new[] { new Renderer { bounds = new Bounds(gate.transform.position, new Vector3(40, 20, 40)) } };

        Assert.Null(access.Refreshed(new[] { gate }, out _));
        Assert.Equal(0, access.FallbackGateCount);
    }

    private static FenceDoor VehicleGate(string name) =>
        new() { name = name, acceptsVehicles = true, transform = { position = new Vector3(100, 3, 200) } };

    private static CityBuildingController BuildingWithBounds(FenceDoor gate, Vector3 size) =>
        new() { Colliders = new[] { new Collider { bounds = new Bounds(gate.transform.position, size) } } };

    private static bool ClearAcrossGate(PublicRoadAccess access, double x, double y) =>
        access.ClearSegment(new Position(x, y, 175), new Position(x, y, 225));

    private static void AssertFallbackWarnings(int firstLog, params FenceDoor[] doors)
    {
        var warnings = Debug.Warnings().Skip(firstLog).ToArray();
        foreach (var door in doors)
            Assert.Single(warnings, line => line.Contains("'" + door.name + "'", StringComparison.Ordinal)
                && line.Contains("fallback", StringComparison.Ordinal));
    }
}
