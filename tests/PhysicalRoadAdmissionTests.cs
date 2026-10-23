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
            System.Array.Empty<RoadBoundary>(), new[] { Exit(40) }, new[] { Enter(20) },
            new[] { Enter(20), Exit(40, 1) }, new[] { Enter(20), Enter(30), Exit(40) },
            new[] { Enter(20), Exit(20) }, new[] { Enter(20), new RoadBoundary(40, false, true, -1) }
        }) Assert.False(PhysicalRoadAdmission.Allows(boundaries, 0, 4));
        Assert.False(PhysicalRoadAdmission.Allows(new[] { Enter(20), Exit(40) }, double.NaN, 4));
        Assert.False(PhysicalRoadAdmission.Allows(new[] { Enter(20), Exit(40) }, 0, double.NaN));
    }
}
