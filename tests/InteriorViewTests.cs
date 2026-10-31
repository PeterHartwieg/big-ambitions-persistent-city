using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;
public sealed class InteriorViewTests
{
    private static readonly Position Door = new(11, 1, 23);
    private static TravelVisibility Travel(int bits) => new((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0,
        (bits & 8) != 0, (bits & 16) != 0, (bits & 32) != 0);
    private static InteriorViewEligibility Shop() => InteriorViewEligibility.Check(true, false, Door, 1);

    [Fact]
    public void MissingSettingsAndTemplateDefaultsAreOffAndBounded()
    {
        foreach (var settings in new[] { new PrototypeSettings(), JsonConvert.DeserializeObject<PrototypeSettings>("{}")! })
        {
            Assert.Equal("off", settings.InteriorCityView);
            Assert.Equal(60, settings.InteriorViewRadiusMetres);
            Assert.Equal(24, settings.InteriorMaximumPedestrians);
            Assert.Equal(12, settings.InteriorMaximumVehicles);
        }
    }

    [Theory]
    [InlineData("-10", 30)] [InlineData("121", 120)] [InlineData("60.5", 60.5)]
    [InlineData("\"Infinity\"", 120)] [InlineData("\"-Infinity\"", 30)]
    [InlineData("\"NaN\"", 60)] [InlineData("true", 60)] [InlineData("null", 60)]
    [InlineData("{}", 60)] [InlineData("[]", 60)] [InlineData("\"72\"", 72)]
    public void RadiusConvertsClampsAndKeepsOtherSettings(string input, double expected)
    {
        var s = JsonConvert.DeserializeObject<PrototypeSettings>("{\"Enabled\":false,\"InteriorViewRadiusMetres\":" + input + "}")!;
        Assert.Equal(expected, s.InteriorViewRadiusMetres); Assert.False(s.Enabled);
    }

    [Theory]
    [InlineData("-10", 0, 0)] [InlineData("1000", 48, 24)] [InlineData("5", 5, 5)]
    [InlineData("5.5", 24, 12)] [InlineData("\"NaN\"", 24, 12)]
    [InlineData("null", 24, 12)] [InlineData("true", 24, 12)] [InlineData("{}", 24, 12)]
    [InlineData("[]", 24, 12)] [InlineData("\"Infinity\"", 48, 24)] [InlineData("\"-Infinity\"", 0, 0)]
    public void SubCapsConvertAndClamp(string input, int people, int cars)
    {
        var s = JsonConvert.DeserializeObject<PrototypeSettings>("{\"InteriorMaximumPedestrians\":" + input + ",\"InteriorMaximumVehicles\":" + input + "}")!;
        Assert.Equal(people, s.InteriorMaximumPedestrians); Assert.Equal(cars, s.InteriorMaximumVehicles);
    }

    [Theory]
    [InlineData("\"on\"", "on")] [InlineData("\"off\"", "off")]
    [InlineData("true", "off")] [InlineData("null", "off")] [InlineData("{}", "off")]
    [InlineData("\"ON\"", "off")] [InlineData("\"bad\"", "off")]
    public void OnlyExplicitOnEnables(string input, string expected)
        => Assert.Equal(expected, JsonConvert.DeserializeObject<PrototypeSettings>("{\"InteriorCityView\":" + input + "}")!.InteriorCityView);

    [Fact]
    public void AdjustmentsAndEffectiveSettingsAreLoggedOnceAfterCrossFieldClamping()
    {
        var s = JsonConvert.DeserializeObject<PrototypeSettings>("{\"InteriorCityView\":true,\"InteriorViewRadiusMetres\":999,\"MaximumPedestrians\":3,\"MaximumVehicles\":2,\"InteriorMaximumVehicles\":999}")!;
        var lines = new List<string>(); s.ReportInteriorSettings(lines.Add); s.ReportInteriorSettings(lines.Add);
        Assert.Equal(5, lines.Count);
        Assert.Equal(3, s.InteriorMaximumPedestrians); Assert.Equal(2, s.InteriorMaximumVehicles);
    }

    [Fact]
    public void OffEqualsEveryLegacyHiddenPredicateAndNeverReadsGeometry()
    {
        var state = new InteriorViewState();
        for (int bits = 0; bits < 64; bits++)
        {
            var travel = Travel(bits);
            state.Refresh(false, travel, this, 1, () => throw new Exception("off must not read"));
            Assert.False(state.Active);
            Assert.Equal(bits != 0, travel.Hidden(state.Active));
            for (int other = 0; other < 8; other++)
            {
                bool paused = (other & 1) != 0, traffic = (other & 2) != 0, running = (other & 4) != 0;
                Assert.Equal(travel.Loading || travel.Inside || paused, travel.StopPedestrian(state.Active, paused));
                Assert.Equal(traffic && !travel.Inside && running, travel.NativeReady(state.Active, traffic, running));
            }
            Assert.Equal(120 * 1.4, state.ReleaseRadius(120, 60));
            Assert.Equal(120, state.Radius(120, 60)); Assert.Equal(48, state.Pedestrians(48, 24)); Assert.Equal(24, state.Vehicles(24, 12));
        }
    }

    [Fact]
    public void EntryExitReentryAndSecondShopUseTheirOwnAnchorWithTwoSecondChecks()
    {
        var state = new InteriorViewState(); var first = new object(); var second = new object(); int reads = 0;
        InteriorViewEligibility Read() { reads++; return Shop(); }
        state.Refresh(true, Travel(8), first, 0, Read);
        for (int i = 1; i < 200; i++) state.Refresh(true, Travel(8), first, i * .01, Read);
        Assert.Equal(1, reads); Assert.True(state.Active);
        Assert.Equal(Door, state.Focus(new Position(900, 0, 900)));
        state.Refresh(true, Travel(8), first, 2, Read); Assert.Equal(2, reads);
        state.Refresh(true, Travel(0), null, 2.1, Read); Assert.False(state.Active);
        Assert.Equal(default, state.Anchor);
        state.Refresh(true, Travel(8), first, 2.2, Read); Assert.Equal(3, reads);
        var door2 = new Position(100, 0, 200);
        state.Refresh(true, Travel(8), second, 2.3, () => new InteriorViewEligibility(door2, InteriorViewFallback.None));
        Assert.Equal(door2, state.Anchor);
    }

    [Fact]
    public void EveryOtherHiddenCauseClosesInsideImmediatelyAndNormalSpeedCanReenter()
    {
        var state = new InteriorViewState();
        foreach (int cause in new[] { 1, 2, 4, 16, 32 })
        {
            state.Refresh(true, Travel(8), this, 0, Shop); Assert.True(state.Active);
            state.Refresh(true, Travel(8 | cause), this, .1, () => throw new Exception("hidden must not inspect"));
            Assert.False(state.Active); Assert.True(Travel(8 | cause).Hidden(state.Active));
            Assert.Equal(default, state.Anchor);
            state.Refresh(true, Travel(8), this, .2, Shop); Assert.True(state.Active);
            state.Reset();
        }
    }

    [Fact]
    public void SaveSuspensionLoadAndDisableLeaveNoRuntimeAnchor()
    {
        var state = new InteriorViewState(); state.Refresh(true, Travel(8), this, 0, Shop);
        state.Suspend(); Assert.False(state.Active); Assert.Equal(default, state.Anchor);
        state.Refresh(true, Travel(8), this, 1, Shop); Assert.True(state.Active);
        state.Reset(); Assert.False(state.Active); Assert.Equal(default, state.Anchor);
        state.Refresh(true, Travel(8), this, 1.1, Shop); Assert.True(state.Active);
        state.Refresh(false, Travel(8), this, 1.2, Shop); Assert.False(state.Active);
    }

    [Theory]
    [InlineData(false, false, 0, 0, 0, 0, (int)InteriorViewFallback.NotShop)]
    [InlineData(true, true, 0, 0, 0, 0, (int)InteriorViewFallback.Elevator)]
    [InlineData(true, false, 0, 0, 0, 3.01, (int)InteriorViewFallback.Height)]
    [InlineData(true, false, 0, 10, 0, 6.99, (int)InteriorViewFallback.Height)]
    [InlineData(true, false, 0, 0, 0, 3, (int)InteriorViewFallback.None)]
    [InlineData(true, false, double.NaN, 0, 0, 0, (int)InteriorViewFallback.Uncertain)]
    [InlineData(true, false, 0, 0, 0, double.NaN, (int)InteriorViewFallback.Uncertain)]
    public void GroundFloorRequiresFiniteEvidenceWithinThreeMetres(bool shop, bool elevator, double x, double y, double z, double floor, int expected)
        => Assert.Equal((InteriorViewFallback)expected, InteriorViewEligibility.Check(shop, elevator, new Position(x, y, z), floor).Fallback);

    [Fact]
    public void ExceptionsFailClosedUntilExitAndEligibilityLossClosesAtRefresh()
    {
        var state = new InteriorViewState(); state.Refresh(true, Travel(8), this, 0, Shop);
        state.Refresh(true, Travel(8), this, 2, () => throw new Exception());
        Assert.False(state.Active); Assert.Equal(InteriorViewFallback.Exception, state.Fallback);
        state.Refresh(true, Travel(8), this, 100, Shop); Assert.False(state.Active);
        state.Reset(); state.Refresh(true, Travel(8), this, 101, Shop); Assert.True(state.Active);
        state.Refresh(true, Travel(8), this, 103, () => InteriorViewEligibility.Check(true, true, Door, 1));
        Assert.False(state.Active); Assert.Equal(InteriorViewFallback.Elevator, state.Fallback);
    }

    [Fact]
    public void InteriorFocusRadiusAndCapsNeverExceedEitherBound()
    {
        var state = new InteriorViewState(); state.Refresh(true, Travel(8), this, 0, Shop);
        Assert.False(Travel(8).Hidden(state.Active));
        Assert.Equal(30, state.Radius(30, 60)); Assert.Equal(60, state.Radius(120, 60));
        Assert.Equal(60, state.ReleaseRadius(120, 60));
        for (int global = 1; global < 257; global++)
        for (int cap = 0; cap < 65; cap++)
        {
            Assert.InRange(state.Pedestrians(global, cap), 0, Math.Min(global, Math.Min(48, cap)));
            Assert.InRange(state.Vehicles(global, cap), 0, Math.Min(global, Math.Min(24, cap)));
        }
    }
}
