using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalVehicleReferenceTests
{
    [Theory]
    [InlineData(.3, 1.7, 0, 1)]
    [InlineData(-.4, 2.8, 1, 0)]
    public void AcquisitionKeepsFrontMarkerOnSavedProgressAndPreservesRoadHeight(double lateral, double nose, double dx, double dz)
    {
        var saved = new Position(100, 2, 80);
        var root = PhysicalVehicleReference.Root(saved, new Position(lateral, .8, nose), new Position(dx, 0, dz));
        Assert.Equal(saved.X, root.X + lateral * dz + nose * dx, 8);
        Assert.Equal(saved.Z, root.Z - lateral * dx + nose * dz, 8);
        Assert.Equal(saved.Y, root.Y);
        Assert.NotEqual(saved, root);
    }

    [Fact]
    public void TurnedBodyPivotAndCentreCannotSubstituteForNativeMarker()
    {
        var corner = new Position(20, 0, 20);
        var root = PhysicalVehicleReference.Root(corner, new Position(0, .7, 2), new Position(1, 0, 0));
        Assert.Equal(18, root.X);
        Assert.Equal(20, root.Z);
        // A centre/pivot measurement would still be two metres short at the reached corner.
        Assert.Equal(2, Position.Distance(root, corner));
    }
}
