using System;
using PersistentCity.Core;
using Xunit;
namespace PersistentCity.Tests;
public sealed class OptionalStopTests
{
    [Theory]
    [InlineData(DwellKind.Break)] [InlineData(DwellKind.ChatOutsideVenue)]
    [InlineData(DwellKind.WindowShopping)] [InlineData(DwellKind.Resting)] [InlineData(DwellKind.Audience)]
    public void StopsRespectExistingCapsAndTheNextCommitment(DwellKind kind)
    {
        var dwell = OptionalStops.Plan("pc:1", kind, "spot", "target", 100, 1000, 1000, false)!.Value;
        Assert.Equal(100 + DwellRules.For(kind).MaximumMinutes, dwell.EndMinute);
        var clipped = OptionalStops.Plan("pc:1", kind, "spot", "target", 100, 1000, 101, false)!.Value;
        Assert.Equal(101, clipped.EndMinute); Assert.False(clipped.ActiveAt(101));
        Assert.Null(OptionalStops.Plan("pc:1", kind, "spot", "target", 100, 10, 100, false));
        Assert.Null(OptionalStops.Plan("pc:1", kind, "spot", "target", 100, 10, 110, true));
    }
    [Fact]
    public void RestoringACommittedStopKeepsItsDeadlineAndIdentity()
    {
        var planned = OptionalStops.Plan("pc:1", DwellKind.Break, "door:1", "street", 100, 8, 120, false)!.Value;
        var restored = new Dwell(planned.CitizenId, planned.Kind, planned.SpotId, planned.StartMinute, planned.EndMinute, planned.FacingId);
        foreach (double minute in new[] { 100.0, 100.25, 104, 107.999, 108, 200 })
            Assert.Equal(OptionalStops.ShouldEnd(planned, minute, false, true), OptionalStops.ShouldEnd(restored, minute, false, true));
        Assert.True(OptionalStops.ShouldEnd(restored, 104, true, true));
        Assert.True(OptionalStops.ShouldEnd(restored, 104, false, false));
        Assert.Equal(108, restored.EndMinute); Assert.Equal("pc:1", restored.CitizenId);
    }
    [Theory]
    [InlineData(DwellKind.WaitForBus)] [InlineData(DwellKind.WaitToCross)]
    [InlineData(DwellKind.QueueAtOpening)] [InlineData(DwellKind.WaitForSomeone)] [InlineData(DwellKind.WaitForPickup)]
    public void EventKindsAreNotReinterpretedAsOptional(DwellKind kind)
        => Assert.Throws<ArgumentException>(() => OptionalStops.Plan("pc:1", kind, "spot", "target", 100, 5, 110, false));
}
