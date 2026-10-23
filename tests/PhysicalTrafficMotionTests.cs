using System;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalTrafficMotionTests
{
    private sealed class Planner : IRoutePlanner
    { public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null; }
    private static PhysicalJourneySample Sample()
    {
        var route = new Route { Legs = new() { new RouteLeg { From = new Position(0, 0, 0), To = new Position(10, 0, 0), Mode = TravelMode.Car, Minutes = 10 } } };
        var person = new CitizenState { Identity = new CitizenIdentity { Id = "test-car" }, Phase = CitizenPhase.Travelling, Journey = route,
            PlaceId = "a", DestinationId = "b", DepartureMinute = 0, NextEventMinute = 10 };
        var state = new CityState { Citizens = new() { person }, Places = new() { new Place { Id = "a" }, new Place { Id = "b", Position = new Position(10, 0, 0) } } };
        return new CitySimulator(state, new Planner()).SampleJourney(person, 0);
    }

    [Fact]
    public void EveryPhysicsStepRecomputesRemainingPermissionFromMeasuredFrontMarker()
    {
        var sample = Sample();
        double first = PhysicalTrafficMotion.MaximumSpeed(sample, new Position(0, 0, 0), 5, 1);
        Assert.Equal(4.85, first, 8);
        Assert.Equal(0, PhysicalTrafficMotion.MaximumSpeed(sample, new Position(first, 0, 0), 5, 1), 8);
        Assert.True(PhysicalTrafficMotion.MaximumSpeed(sample, new Position(first, 0, 0), 6, 1) > 0);
    }

    [Fact]
    public void HeadCanReachNativeEndpointWithoutPermanentSetback()
    {
        var sample = Sample();
        Assert.Equal(2.5, PhysicalTrafficMotion.MaximumSpeed(sample, new Position(9.95, 0, 0), 10, .02), 8);
        Assert.Equal(0, PhysicalTrafficMotion.MaximumSpeed(sample, new Position(10, 0, 0), 10, .02));
        Assert.Equal(0, PhysicalTrafficMotion.MaximumSpeed(sample, new Position(10.2, 0, 0), 10, .02));
    }

    [Fact]
    public void LimitReducesNativeCommandPreservingDirectionVerticalForceAndStrongerStop()
    {
        var velocity = new Position(6, 1, 8); var native = new Position(0, 2, 0);
        var limited = PhysicalTrafficMotion.LimitVelocityChange(velocity, native, 5);
        Assert.Equal(-3, limited.X, 8); Assert.Equal(2, limited.Y); Assert.Equal(-4, limited.Z, 8);
        var nativeStop = new Position(-6, 2, -8);
        Assert.Equal(nativeStop, PhysicalTrafficMotion.LimitVelocityChange(velocity, nativeStop, 5));
        var nativeSlow = new Position(-5.4, 0, -7.2);
        Assert.Equal(nativeSlow, PhysicalTrafficMotion.LimitVelocityChange(velocity, nativeSlow, 5));
    }

    [Fact]
    public void ZeroPermissionStopsHorizontalCommandAndInvalidGeometryFailsClosed()
    {
        var stop = PhysicalTrafficMotion.LimitVelocityChange(new Position(2, 0, 3), new Position(1, 4, 1), 0);
        Assert.Equal(new Position(-2, 4, -3), stop);
        Assert.Equal(0, PhysicalTrafficMotion.MaximumSpeed(Sample(), new Position(0, 0, 0), 5, 0));
        Assert.Equal(0, PhysicalTrafficMotion.MaximumSpeed(Sample(), new Position(double.NaN, 0, 0), 5, .02));
        Assert.Throws<ArgumentException>(() => PhysicalTrafficMotion.LimitVelocityChange(new Position(0, 0, 0), new Position(0, 0, 0), double.NaN));
    }

    [Fact]
    public void StoppedCarAtInteriorPermissionCannotAdvanceFrontMarkerByRotating()
    {
        double diagonal = Math.Sqrt(2);
        var rotation = PhysicalTrafficMotion.LimitRotation(new Position(diagonal, 0, diagonal),
            new Position(0, 1, 0), new Position(0, 0, 1), -Math.PI / 4, 0);
        Assert.Equal(0, rotation.Fraction); Assert.Equal(0, rotation.ReservedAdvance);
        Assert.Equal(new Position(0, 0, 0), PhysicalTrafficMotion.LimitVelocityChange(new Position(0, 0, 0), new Position(0, 0, 0), 0));
    }

    [Fact]
    public void RotationAndTranslationShareOneAdvanceAllowanceWithoutWeakeningNativeStop()
    {
        double diagonal = Math.Sqrt(2), allowance = .7, step = .02;
        var rotation = PhysicalTrafficMotion.LimitRotation(new Position(diagonal, 0, diagonal),
            new Position(0, 1, 0), new Position(0, 0, 1), -Math.PI / 4, allowance);
        Assert.Equal(1, rotation.Fraction);
        double advanceFromRotation = 2 - diagonal;
        Assert.True(rotation.ReservedAdvance >= advanceFromRotation);
        double maximumSpeed = (allowance - rotation.ReservedAdvance) / step;
        var change = PhysicalTrafficMotion.LimitVelocityChange(new Position(0, 0, 10), new Position(0, 0, 0), maximumSpeed);
        Assert.True((10 + change.Z) * step + advanceFromRotation <= allowance);
        var nativeStop = new Position(0, 0, -10);
        Assert.Equal(nativeStop, PhysicalTrafficMotion.LimitVelocityChange(new Position(0, 0, 10), nativeStop, maximumSpeed));
    }

    [Fact]
    public void RotationArcPeakIsConstrainedEvenWhenFinalFrontProjectionReturnsToItsStart()
    {
        double diagonal = Math.Sqrt(2), allowance = .2;
        var rotation = PhysicalTrafficMotion.LimitRotation(new Position(diagonal, 0, diagonal),
            new Position(0, 1, 0), new Position(0, 0, 1), -Math.PI / 2, allowance);
        Assert.InRange(rotation.Fraction, .001, .499);
        for (int i = 0; i <= 200; i++)
        {
            double angle = -Math.PI / 2 * rotation.Fraction * i / 200;
            double forwardShift = diagonal * Math.Cos(angle) - diagonal * Math.Sin(angle) - diagonal;
            Assert.True(forwardShift <= allowance, "Intermediate rotation crossed the queue limit.");
        }
    }

    [Fact]
    public void FarEndpointAllowsSafeRotationPrefixAndNoPermanentTranslationSetback()
    {
        var sample = Sample();
        double allowance = PhysicalTrafficMotion.MaximumAdvance(sample, new Position(9.95, 0, 0), 10);
        Assert.Equal(.05, allowance, 8);
        var rotation = PhysicalTrafficMotion.LimitRotation(new Position(Math.Sqrt(2), 0, Math.Sqrt(2)),
            new Position(0, 1, 0), new Position(1, 0, 0), Math.PI / 4, allowance);
        Assert.InRange(rotation.Fraction, .0001, .9999);
        Assert.InRange(rotation.ReservedAdvance, 0, allowance);
        Assert.Equal(0, PhysicalTrafficMotion.LimitRotation(new Position(Math.Sqrt(2), 0, Math.Sqrt(2)),
            new Position(0, 1, 0), new Position(1, 0, 0), Math.PI / 4, 0).Fraction);
        // A turn that moves the front marker away from its limit remains fully native.
        Assert.Equal(1, PhysicalTrafficMotion.LimitRotation(new Position(Math.Sqrt(2), 0, Math.Sqrt(2)),
            new Position(0, 1, 0), new Position(0, 0, 1), Math.PI / 4, 0).Fraction);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(0, .8, .6)]
    [InlineData(.5, .7, .5)]
    public void PrefixBoundCoversTiltedLocalUpAxes(double ax, double ay, double az)
    {
        double axisLength = Math.Sqrt(ax * ax + ay * ay + az * az); ax /= axisLength; ay /= axisLength; az /= axisLength;
        var marker = new Position(1.3, .6, 2); double allowance = .15, directionLength = Math.Sqrt(1.25);
        var result = PhysicalTrafficMotion.LimitRotation(marker, new Position(ax, ay, az), new Position(1, 0, .5), 1.2, allowance);
        // Independent 3-D Rodrigues samples check the scalar arc envelope on level and tilted bodies.
        double dot = ax * marker.X + ay * marker.Y + az * marker.Z;
        for (int i = 0; i <= 300; i++)
        {
            double angle = 1.2 * result.Fraction * i / 300, cosine = Math.Cos(angle), sine = Math.Sin(angle);
            double x = marker.X * cosine + (ay * marker.Z - az * marker.Y) * sine + ax * dot * (1 - cosine);
            double z = marker.Z * cosine + (ax * marker.Y - ay * marker.X) * sine + az * dot * (1 - cosine);
            double advance = ((x - marker.X) + .5 * (z - marker.Z)) / directionLength;
            Assert.True(advance <= allowance + 1e-10, "A tilted-body command advanced beyond queue permission.");
        }
    }
}
