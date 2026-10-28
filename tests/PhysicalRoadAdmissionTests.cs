using PersistentCity.Core;
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
        // An intermediate node at 30 may have no native intersection reference at all.
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
        // Exact read-only r4 evidence: a leading native exit at the curb-connected
        // road block's start, followed by five complete native junction passages.
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
}
