using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PublicRoadGateShapeTests
{
    private static readonly Position Door = new(100, 0, 200);
    private static GateExtent Box(double x0, double y0, double z0, double x1, double y1, double z1) => new(new(x0, y0, z0), new(x1, y1, z1));
    private static void Same(Position expected, Position actual)
    {
        Assert.Equal(expected.X, actual.X, 9); Assert.Equal(expected.Y, actual.Y, 9); Assert.Equal(expected.Z, actual.Z, 9);
    }

    [Fact]
    public void ColliderGeometryKeepsTheExpandedColliderBounds()
    {
        bool askedRenderer = false, askedBuilding = false;
        var source = GateShape.Choose(Door, Box(98, 0, 199, 104, 3, 201),
            () => { askedRenderer = true; return Box(0, 0, 0, 500, 50, 500); },
            () => { askedBuilding = true; return Box(0, 0, 0, 500, 50, 500); }, out var gate);
        Assert.Equal(GateShapeSource.Colliders, source);
        Same(new(97.5, -1, 198.5), gate.Min);
        Same(new(104.5, 4, 201.5), gate.Max);
        Assert.False(askedRenderer);
        Assert.False(askedBuilding);
    }

    [Fact]
    public void GateWithoutCollidersUsesRendererBounds()
    {
        var source = GateShape.Choose(Door, null, () => Box(99, 0, 198, 103, 2.5, 202), () => Box(0, 0, 0, 500, 50, 500), out var gate);
        Assert.Equal(GateShapeSource.Renderers, source);
        Same(new(98.5, -1, 197.5), gate.Min);
        Same(new(103.5, 3.5, 202.5), gate.Max);
    }

    [Fact]
    public void GateWithoutCollidersOrRenderersUsesTheBuildingFootprint()
    {
        var source = GateShape.Choose(Door, Box(1, 1, 1, 1, 1, 1), () => null, () => Box(90, 0, 190, 120, 12, 215), out var gate);
        Assert.Equal(GateShapeSource.Building, source);
        Same(new(89.5, -1, 189.5), gate.Min);
        Same(new(120.5, 13, 215.5), gate.Max);
    }

    [Fact]
    public void UnusableShapesFallThroughToTheNextSource()
    {
        var nan = Box(double.NaN, 0, 0, 1, 1, 1);
        var inverted = Box(5, 5, 5, 1, 1, 1);
        Assert.Equal(GateShapeSource.Building,
            GateShape.Choose(Door, nan, () => inverted, () => Box(90, 0, 190, 110, 5, 210), out _));
    }

    [Fact]
    public void GateWithNoShapeIsCountedAndKeepsABoundedFallbackBox()
    {
        var source = GateShape.Choose(Door, null, () => null, () => null, out var gate);
        Assert.Equal(GateShapeSource.Fallback, source);
        Same(new(80, -10, 180), gate.Min);
        Same(new(120, 10, 220), gate.Max);
        Assert.True(TransportAccessGeometry.IntersectsBounds(new(100, 0, 150), new(100, 0, 250), gate.Min, gate.Max));
        Assert.False(TransportAccessGeometry.IntersectsBounds(new(200, 0, 0), new(200, 0, 400), gate.Min, gate.Max));

        var tally = new GateShapeTally();
        tally.Add(GateShapeSource.Colliders, "Gate A");
        tally.Add(source, "Gate B");
        tally.Add(GateShapeSource.Renderers, "Gate C");
        Assert.Equal(1, tally.FallbackGates);
        Assert.Equal(new[] { "Gate B" }, tally.FallbackDoors);
        Assert.Equal(1, tally.Count(GateShapeSource.Colliders));
        Assert.Equal(1, tally.Count(GateShapeSource.Renderers));
        var report = tally.Report(null);
        Assert.NotNull(report);
        Assert.Contains("1 without a shape", report);
        Assert.Contains("Gate B", report);
    }

    [Fact]
    public void ReportIsSilentWhenEveryGateHasCollidersOrNothingChanged()
    {
        var colliders = new GateShapeTally();
        colliders.Add(GateShapeSource.Colliders, "Gate A");
        Assert.Null(colliders.Report(null));

        GateShapeTally Fallback(params string[] doors)
        {
            var tally = new GateShapeTally();
            foreach (var door in doors) tally.Add(GateShapeSource.Fallback, door);
            return tally;
        }
        Assert.Null(Fallback("Gate B", "Gate C").Report(Fallback("Gate C", "Gate B")));
        Assert.NotNull(Fallback("Gate B", "Gate D").Report(Fallback("Gate B", "Gate C")));
        Assert.NotNull(Fallback("Gate B").Report(colliders));
    }
}
