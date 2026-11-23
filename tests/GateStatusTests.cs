using System;
using System.Collections.Generic;
using System.Linq;
using Buildings;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using Xunit;

namespace PersistentCity.Tests;

public sealed class GateStatusTests
{
    [Fact]
    public void ZeroFallbackTallyLogsOnceAtConstructionAndNotAgainWhileUnchanged()
    {
        var logs = new List<string>();
        var tally = new PublicRoadAccess.FallbackTally(logs.Add);
        var doors = new[] { ColliderGate("solid gate", 100) };

        var access = new PublicRoadAccess(doors, tally);
        var line = Assert.Single(logs);
        Assert.StartsWith("[Persistent City] Vehicle gates with no collider, renderer or building bounds: 0 using the 40x20x40 m fallback.", line, StringComparison.Ordinal);

        Assert.Null(access.Refreshed(doors, out _));
        _ = new PublicRoadAccess(doors, tally);
        _ = new PublicRoadAccess(Array.Empty<FenceDoor>(), tally);
        Assert.Single(logs);
    }

    [Fact]
    public void ChangedTallyLogsOnceAndForgettingTheTallyLogsItAgainOnTheNextLoad()
    {
        var logs = new List<string>();
        var tally = new PublicRoadAccess.FallbackTally(logs.Add);
        var shapeless = Gate("shapeless gate", 300);
        var doors = new[] { ColliderGate("solid gate", 100), shapeless };

        var access = new PublicRoadAccess(doors, tally);
        Assert.Contains("bounds: 1 using the 40x20x40 m fallback: shapeless gate", Assert.Single(logs), StringComparison.Ordinal);
        _ = new PublicRoadAccess(doors, tally);
        Assert.Single(logs);

        shapeless.Colliders = new[] { new Collider { bounds = new Bounds(shapeless.transform.position, new Vector3(4, 2, 2)) } };
        var changed = access.Refreshed(doors, out _);
        Assert.NotNull(changed);
        Assert.Equal(2, logs.Count);
        Assert.Contains("bounds: 0 using", logs[1], StringComparison.Ordinal);
        Assert.Null(changed!.Refreshed(doors, out _));
        Assert.Equal(2, logs.Count);

        tally.Forget();
        _ = new PublicRoadAccess(doors, tally);
        Assert.Equal(3, logs.Count);
        Assert.Contains("bounds: 0 using", logs[2], StringComparison.Ordinal);
    }

    [Fact]
    public void StatusCountsGatesBySourceWithoutListingRows()
    {
        var text = Report(Access(), Roads(), "", "");

        Assert.Contains("gates=4 collider=1 renderer=1 footprint=1 fallback=1", text, StringComparison.Ordinal);
        Assert.Contains("road_snapshot=ready nodes=4", text, StringComparison.Ordinal);
        Assert.DoesNotContain("door=", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceFilterListsExpandedBoxBuildingAndPublicRoadOverlap()
    {
        var access = Access();
        var footprint = Report(access, Roads(), "footprint", "");
        var collider = Report(access, Roads(), "collider", "");

        Assert.Contains("filter=footprint matched=1 shown=1 omitted=0", footprint, StringComparison.Ordinal);
        Assert.Contains("door=footprint gate | building=yard | source=footprint | min=(693.5, -1, 195.5) | max=(706.5, 7, 204.5) | public_road=yes segments=1 first=0->1 road_id=100",
            footprint, StringComparison.Ordinal);
        Assert.Contains("door=collider gate | building=none | source=collider | min=(97.5, 1, 198.5) | max=(102.5, 5, 201.5) | public_road=no",
            collider, StringComparison.Ordinal);
        Assert.Contains("public_road=unknown", Report(access, null, "fallback", ""), StringComparison.Ordinal);
    }

    [Fact]
    public void NearFilterKeepsGatesWithinTheRadiusInXZ()
    {
        var text = Report(Access(), Roads(), "near", "310,200,15");

        Assert.Contains("filter=near 310,200,15 matched=1 shown=1 omitted=0", text, StringComparison.Ordinal);
        Assert.Contains("door=fallback gate | building=none | source=fallback | min=(280, -7, 180) | max=(320, 13, 220)", text, StringComparison.Ordinal);
        Assert.Contains("matched=0", Report(Access(), Roads(), "near", "1000,1000,5"), StringComparison.Ordinal);
    }

    [Fact]
    public void ListingIsCappedAndPutsRoadOverlapsFirst()
    {
        var doors = Enumerable.Range(0, GateStatusReport.MaximumRows + 5).Select(i => (FenceDoor)Gate("gate " + i.ToString("D2"), i * 100)).ToArray();
        var access = new PublicRoadAccess(doors, new PublicRoadAccess.FallbackTally(_ => { }));
        var roads = new List<TransportRoadNode>
        {
            new() { Position = new Position(4400, 3, 150), AllowsCar = true, Successors = new[] { 1 } },
            new() { Position = new Position(4400, 3, 250), AllowsCar = true }
        };

        var text = Report(access, roads, "all", "");
        var rows = text.Split('\n').Where(line => line.StartsWith("door=", StringComparison.Ordinal)).ToArray();

        Assert.Contains("matched=45 shown=40 omitted=5", text, StringComparison.Ordinal);
        Assert.Equal(GateStatusReport.MaximumRows, rows.Length);
        Assert.StartsWith("door=gate 44 ", rows[0], StringComparison.Ordinal);
        Assert.Contains("public_road=yes", rows[0], StringComparison.Ordinal);
        Assert.StartsWith("door=gate 00 ", rows[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("road", "")]
    [InlineData("near", "1,2")]
    [InlineData("near", "1,2,-3")]
    [InlineData("near", "a,2,3")]
    [InlineData("footprint", "extra")]
    public void BadArgumentsAreRefused(string detail, string value)
    {
        Assert.False(GateStatusReport.TryParse(detail, value, out _, out _, out _, out _, out _, out string error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void ReadingsDoNotChangeAdmission()
    {
        var access = Access();
        bool before = access.ClearSegment(new Position(700, 3, 150), new Position(700, 3, 250));

        _ = Report(access, Roads(), "all", "");

        Assert.False(before);
        Assert.Equal(before, access.ClearSegment(new Position(700, 3, 150), new Position(700, 3, 250)));
        Assert.Equal(4, access.Readings().Count);
    }

    private static string Report(PublicRoadAccess access, IReadOnlyList<TransportRoadNode>? roads, string detail, string value)
    {
        Assert.True(GateStatusReport.TryParse(detail, value, out var source, out bool list, out double x, out double z, out double radius, out string error), error);
        return GateStatusReport.Format(access.Readings(), door => door.GetComponentInParent<CityBuildingController>(true) != null ? "yard" : "none",
            roads, roads == null ? "none" : "ready", index => index + 100, source, list, x, z, radius);
    }

    private static PublicRoadAccess Access()
    {
        var renderer = Gate("renderer gate", 500);
        renderer.Renderers = new[] { new Renderer { bounds = new Bounds(renderer.transform.position, new Vector3(2, 2, 2)) } };
        var footprint = Gate("footprint gate", 700);
        footprint.transform.parent = new Transform
        {
            Component = new CityBuildingController { Colliders = new[] { new Collider { bounds = new Bounds(footprint.transform.position, new Vector3(12, 6, 8)) } } }
        };
        FenceDoor[] doors = { ColliderGate("collider gate", 100), Gate("fallback gate", 300), renderer, footprint };
        return new PublicRoadAccess(doors, new PublicRoadAccess.FallbackTally(_ => { }));
    }

    private static List<TransportRoadNode> Roads() => new()
    {
        new() { Position = new Position(690, 3, 200), AllowsCar = true, Successors = new[] { 1 } },
        new() { Position = new Position(710, 3, 200), AllowsCar = true },
        new() { Position = new Position(90, 3, 300), AllowsCar = true, Successors = new[] { 3 } },
        new() { Position = new Position(110, 3, 300), AllowsCar = true }
    };

    private static FenceDoor ColliderGate(string name, float x)
    {
        var gate = Gate(name, x);
        gate.Colliders = new[] { new Collider { bounds = new Bounds(gate.transform.position, new Vector3(4, 2, 2)) } };
        return gate;
    }

    private static FenceDoor Gate(string name, float x) =>
        new() { name = name, acceptsVehicles = true, transform = { position = new Vector3(x, 3, 200) } };
}
