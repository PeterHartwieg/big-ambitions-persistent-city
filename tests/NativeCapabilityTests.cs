using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Tests;

[CollectionDefinition(Collection, DisableParallelization = true)]
public sealed class F8aSerial { public const string Collection = "F8a serial"; }

[Collection(F8aSerial.Collection)]
public sealed class NativeCapabilityTests
{
    private readonly ITestOutputHelper output;
    public NativeCapabilityTests(ITestOutputHelper output) => this.output = output;

    private static IEnumerable<CapabilityId> Ids => NativeCapabilities.All.Select(d => d.Id);

    private static CapabilityRegistry AllActive()
    {
        var registry = new CapabilityRegistry();
        foreach (var id in Ids) Assert.Equal(CapabilityTransition.Applied, registry.Detect(id, CapabilityState.Active));
        return registry;
    }

    [Fact]
    public void DefinitionsFollowTheD2Table()
    {
        Assert.Equal(Enum.GetValues<CapabilityId>(), NativeCapabilities.All.Select(d => d.Id));
        Assert.Equal(NativeCapabilities.Count, NativeCapabilities.All.Select(d => d.Name).Distinct(StringComparer.Ordinal).Count());
        foreach (var d in NativeCapabilities.All)
        {
            Assert.True(NativeCapabilities.TryParse(d.Name, out var parsed)); Assert.Equal(d.Id, parsed);
            Assert.NotEqual(CapabilityScope.None, d.Scope);
            Assert.False(string.IsNullOrWhiteSpace(d.Fallback)); Assert.False(string.IsNullOrWhiteSpace(d.NativeContract));
            Assert.Equal(d.Requirement != CapabilityRequirement.Optional, d.Scope == CapabilityScope.WholeCity);
            if (d.Requirement == CapabilityRequirement.RequiredWhenPresent) Assert.NotNull(d.Companion);
        }
        Assert.False(NativeCapabilities.TryParse("food.Authority", out _));
        string Required(CapabilityRequirement r) => string.Join(",", NativeCapabilities.All.Where(d => d.Requirement == r).Select(d => d.Name));
        Assert.Equal("food.authority,clock.observe,clock.city,rlo.donor", Required(CapabilityRequirement.Required));
        Assert.Equal("clock.drivenrides", Required(CapabilityRequirement.RequiredWhenPresent));
        Assert.Equal(new[] { "food.dynamic", "interior.view" }, NativeCapabilities.All.Where(d => d.OffByDefault).Select(d => d.Name));
        Assert.Equal(CapabilityRecheck.CompanionRevision, NativeCapabilities.Get(CapabilityId.VendorStreetFood).Recheck);
    }

    [Fact]
    public void Transitions()
    {
        var r = new CapabilityRegistry();
        Assert.All(Ids, id => Assert.Equal(CapabilityState.NotChecked, r.State(id)));
        Assert.Equal(0, r.Revision);

        Assert.Equal(CapabilityTransition.Applied, r.Detect(CapabilityId.FoodAuthority, CapabilityState.Active, "ignored"));
        Assert.Null(r.Reason(CapabilityId.FoodAuthority));
        Assert.Equal(CapabilityTransition.Applied, r.Detect(CapabilityId.ResearchDialog, CapabilityState.Off, "dialog changed"));
        Assert.Equal(CapabilityTransition.Applied, r.Detect(CapabilityId.VendorStreetFood, CapabilityState.Absent, "no VendorApi"));
        Assert.Equal(3, r.Revision);
        Assert.Equal(CapabilityTransition.Rejected, r.Detect(CapabilityId.FoodAuthority, CapabilityState.Off, "late"));
        Assert.Equal(CapabilityTransition.Rejected, r.Detect(CapabilityId.ResearchDialog, CapabilityState.Active));
        Assert.Equal(CapabilityTransition.Rejected, r.Detect(CapabilityId.ClockCity, CapabilityState.Absent));
        Assert.Equal(CapabilityState.NotChecked, r.State(CapabilityId.ClockCity));
        Assert.Equal(3, r.Revision);

        Assert.Equal(CapabilityTransition.Rejected, r.Fail(CapabilityId.ClockCity, "not checked yet"));
        Assert.Equal(CapabilityTransition.Rejected, r.Fail(CapabilityId.VendorStreetFood, "absent"));
        Assert.Equal(CapabilityTransition.Applied, r.Fail(CapabilityId.FoodAuthority, "ProcessCustomer changed"));
        Assert.Equal(CapabilityTransition.Unchanged, r.Fail(CapabilityId.FoodAuthority, "second failure"));
        Assert.Equal(CapabilityState.Off, r.State(CapabilityId.FoodAuthority));
        Assert.Equal("ProcessCustomer changed", r.Reason(CapabilityId.FoodAuthority));
        Assert.Equal(4, r.Revision);

        Assert.Throws<ArgumentException>(() => r.Detect(CapabilityId.ClockCity, CapabilityState.Off, " "));
        Assert.Throws<ArgumentException>(() => r.Fail(CapabilityId.FoodAuthority, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => r.Detect(CapabilityId.ClockCity, CapabilityState.NotChecked));
        Assert.Throws<ArgumentOutOfRangeException>(() => r.State((CapabilityId)200));
        Assert.Equal(4, r.Revision);
    }

    [Fact]
    public void OffIsStickyUntilTheDeclaredRecheck()
    {
        var r = AllActive();
        Assert.Equal(CapabilityTransition.Applied, r.Fail(CapabilityId.ResearchBilling, "foreign prefix by other.mod"));
        long revision = r.Revision;
        Assert.Equal(CapabilityTransition.Rejected, r.Detect(CapabilityId.ResearchBilling, CapabilityState.Active));
        Assert.Equal(CapabilityTransition.Rejected, r.Recheck(CapabilityId.ResearchBilling, CapabilityRecheck.CompanionRevision, CapabilityState.Active));
        Assert.Equal(CapabilityTransition.Unchanged, r.Fail(CapabilityId.ResearchBilling, "again"));
        Assert.Equal(CapabilityState.Off, r.State(CapabilityId.ResearchBilling));
        Assert.Equal(revision, r.Revision);
        Assert.Equal(CapabilityTransition.Applied, r.Recheck(CapabilityId.ResearchBilling, CapabilityRecheck.CityLoad, CapabilityState.Active));
        Assert.Equal(CapabilityState.Active, r.State(CapabilityId.ResearchBilling));
        Assert.Null(r.Reason(CapabilityId.ResearchBilling));
        Assert.Equal(CapabilityTransition.Unchanged, r.Recheck(CapabilityId.ResearchBilling, CapabilityRecheck.CityLoad, CapabilityState.Active));

        var v = new CapabilityRegistry();
        Assert.Equal(CapabilityTransition.Applied, v.Detect(CapabilityId.VendorStreetFood, CapabilityState.Absent, "VendorApi missing"));
        Assert.Equal(CapabilityTransition.Rejected, v.Recheck(CapabilityId.VendorStreetFood, CapabilityRecheck.CityLoad, CapabilityState.Active));
        Assert.Equal(CapabilityTransition.Rejected, v.Detect(CapabilityId.VendorStreetFood, CapabilityState.Active));
        Assert.Equal(CapabilityState.Absent, v.State(CapabilityId.VendorStreetFood));
        Assert.Equal(CapabilityTransition.Applied, v.Recheck(CapabilityId.VendorStreetFood, CapabilityRecheck.CompanionRevision, CapabilityState.Active));
        Assert.Equal(CapabilityTransition.Applied, v.Recheck(CapabilityId.VendorStreetFood, CapabilityRecheck.CompanionRevision, CapabilityState.Off, "contract 2"));
        Assert.Equal("contract 2", v.Reason(CapabilityId.VendorStreetFood));
        Assert.Equal(CapabilityTransition.Rejected, v.Recheck(CapabilityId.FoodAuthority, CapabilityRecheck.CityLoad, CapabilityState.Active));
        Assert.Equal(CapabilityTransition.Rejected, r.Recheck(CapabilityId.ClockCity, CapabilityRecheck.CityLoad, CapabilityState.Absent));
    }

    [Fact]
    public void ARequiredOffIsAStartRefusalDistinctFromCorruptionAndFromAnOlderCity()
    {
        var fresh = new CapabilityRegistry();
        var waiting = fresh.Start();
        Assert.Equal(CapabilityStartStatus.Waiting, waiting.Status);
        Assert.Equal(CapabilityId.FoodAuthority, waiting.Capability);

        var r = AllActive();
        Assert.Equal(CapabilityStartStatus.Ready, r.Start().Status);
        Assert.Null(r.Start().Capability);
        r.Fail(CapabilityId.FoodAuthority, "RetailBusinessSimulator.ProcessCustomer changed");
        var refused = r.Start();
        Assert.Equal(CapabilityStartStatus.Refused, refused.Status);
        Assert.Equal(CapabilityId.FoodAuthority, refused.Capability);
        Assert.Contains("food.authority is off: RetailBusinessSimulator.ProcessCustomer changed", refused.Message);
        Assert.Contains("The saved city is kept unchanged.", refused.Message);

        var state = ChangeJournalTests.Fixture(5150);
        var loaded = SaveCodec.Read(SaveCodec.Write(state));
        Assert.Equal(SaveReadStatus.Loaded, loaded.Status);
        var corrupt = SaveCodec.Read("{\"Schema\":35,\"f\":\"pcc-c\",\"v\":1,\"k\":[");
        Assert.Equal(SaveReadStatus.Invalid, corrupt.Status);
        Assert.NotEqual(corrupt.Error, refused.Message);
        Assert.NotEqual(SaveCodec.DevelopmentSchemaMessage, refused.Message);
        Assert.DoesNotContain("start a new city", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("corrupt", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invalid", refused.Message, StringComparison.OrdinalIgnoreCase);

        var rlo = AllActiveExcept(CapabilityId.RloDonor, CapabilityState.Absent, "RealLifeOverhaul not loaded");
        var missing = rlo.Start();
        Assert.Equal(CapabilityStartStatus.Refused, missing.Status);
        Assert.Contains("rlo.donor is missing (Real Life Overhaul (RealLifeOverhaul) is required)", missing.Message);
        rlo.Fail(CapabilityId.ClockObserve, "observer owner taken");
        Assert.Equal(CapabilityId.ClockObserve, rlo.Start().Capability);

        Assert.Equal(CapabilityStartStatus.Ready,
            AllActiveExcept(CapabilityId.ClockDrivenRides, CapabilityState.Absent, null).Start().Status);
        var rides = AllActiveExcept(CapabilityId.ClockDrivenRides, CapabilityState.Off, "Unsupported Driven Rides clock contract.").Start();
        Assert.Equal(CapabilityStartStatus.Refused, rides.Status);
        Assert.Contains("clock.drivenrides is off: Unsupported Driven Rides clock contract.", rides.Message);
    }

    private static CapabilityRegistry AllActiveExcept(CapabilityId id, CapabilityState state, string? reason)
    {
        var registry = new CapabilityRegistry();
        foreach (var each in Ids) registry.Detect(each, each == id ? state : CapabilityState.Active, each == id ? reason : null);
        return registry;
    }

    [Fact]
    public void AnOptionalOffDisablesOnlyItsListedScope()
    {
        var all = AllActive();
        Assert.Equal(CapabilityScope.None, all.DisabledScopes);
        foreach (var d in NativeCapabilities.All.Where(d => d.Requirement == CapabilityRequirement.Optional))
        {
            var r = AllActive();
            Assert.Equal(CapabilityTransition.Applied, r.Fail(d.Id, d.Name + " changed"));
            Assert.Equal(d.Scope, r.DisabledScopes);
            Assert.Equal(CapabilityStartStatus.Ready, r.Start().Status);
            Assert.False(r.IsEnabled(d.Scope));
            foreach (CapabilityScope scope in Enum.GetValues<CapabilityScope>())
                if (scope != CapabilityScope.None && (scope & d.Scope) == 0) Assert.True(r.IsEnabled(scope), d.Name + " disabled " + scope);
        }
        var research = AllActive();
        research.Fail(CapabilityId.ResearchDelivery, "TextMessage constructor changed");
        Assert.Equal(CapabilityScope.MarketResearch, research.DisabledScopes);
        Assert.Equal(CapabilityScope.VehicleScopes,
            AllActiveExcept(CapabilityId.VendorStreetFood, CapabilityState.Absent, "no VendorApi").DisabledScopes);
        Assert.Equal(CapabilityScope.None, AllActiveExcept(CapabilityId.ClockDrivenRides, CapabilityState.Absent, null).DisabledScopes);
        Assert.False(all.IsEnabled(CapabilityScope.None));
    }

    [Fact]
    public void DeterministicReport()
    {
        var a = new CapabilityRegistry();
        foreach (var id in Ids) a.Detect(id, CapabilityState.Active);
        a.Fail(CapabilityId.ResearchBilling, "foreign prefix by\r\nother.mod");
        a.Recheck(CapabilityId.VendorStreetFood, CapabilityRecheck.CompanionRevision, CapabilityState.Absent, "VendorApi missing");
        var b = new CapabilityRegistry();
        foreach (var id in Ids.Reverse())
            b.Detect(id, id == CapabilityId.VendorStreetFood ? CapabilityState.Absent : CapabilityState.Active,
                id == CapabilityId.VendorStreetFood ? "VendorApi missing" : null);
        b.Fail(CapabilityId.ResearchBilling, "foreign prefix by\r\nother.mod");

        var report = new CapabilityReport();
        string first = report.Status(a, "0.9.1");
        Assert.Same(first, report.Status(a, "0.9.1"));
        Assert.Equal(first, new CapabilityReport().Status(b, "0.9.1"));
        Assert.NotEqual(first, report.Status(a, "0.9.2"));

        var lines = first.TrimEnd('\n').Split('\n');
        Assert.Equal(1 + NativeCapabilities.Count, lines.Length);
        Assert.Equal("Persistent City capabilities (game 0.9.1): start ready", lines[0]);
        Assert.Equal(NativeCapabilities.All.Select(d => d.Name), lines.Skip(1).Select(l => l.Split(' ')[0]));
        Assert.Contains("research.billing optional off: foreign prefix by other.mod | scope: market research", lines);
        Assert.Contains("vendor.streetfood optional absent: VendorApi missing | scope: vehicle scopes", lines);
        Assert.Contains("food.authority required active | scope: whole city", lines);
        Assert.Contains("clock.drivenrides required-when-present active | scope: whole city", lines);
        Assert.DoesNotContain("compatible", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verified", first, StringComparison.OrdinalIgnoreCase);

        a.Fail(CapabilityId.ClockCity, "RunMainGameTick(float) changed");
        string after = report.Status(a, "0.9.1");
        Assert.StartsWith("Persistent City capabilities (game 0.9.1): start refused (clock.city)\n", after);
        Assert.Contains("clock.city required off: RunMainGameTick(float) changed | scope: whole city\n", after);

        var unchecked_ = new CapabilityReport().Status(new CapabilityRegistry(), "x");
        Assert.Contains("food.dynamic optional not checked (off by default) | scope: dynamic shops", unchecked_);
        Assert.StartsWith("Persistent City capabilities (game x): start waiting (food.authority)\n", unchecked_);

        string show = report.Show(a, CapabilityId.ClockCity);
        Assert.Equal("clock.city required off: RunMainGameTick(float) changed | scope: whole city\n"
            + "native contract: cityclock RunMainGameTick(float) prefixes (H3)\n"
            + "hook: hart.persistentcity.cityclock.v1 GameManager.RunMainGameTick(Single) prefix\n"
            + "fallback: Not started.\nre-check: city load\n", show);
        Assert.Equal(23, report.Show(a, CapabilityId.FoodDynamic).Split('\n').Count(l => l.StartsWith("hook: persistentcity.food-contract ")));
        Assert.Contains("companion: Driven Rides (DrivenRides.Ride)", report.Show(a, CapabilityId.ClockDrivenRides));
    }

    [Fact]
    public void RegistryUpdateAndReportCost()
    {
        const int Warm = 1_000, Samples = 20_000;
        var registry = AllActive();
        var report = new CapabilityReport();
        var rows = new List<string>();
        var times = new double[Samples];
        double tick = 1000.0 / Stopwatch.Frequency;

        long Run(string arm, Action body)
        {
            for (int i = 0; i < Warm; i++) body();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Samples; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                body();
                times[i] = (Stopwatch.GetTimestamp() - t0) * tick;
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            if (bytes != 0)
            {
                before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < Samples; i++) body();
                bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            var sorted = times.OrderBy(x => x).ToArray();
            double P(double q) => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(q * sorted.Length) - 1)];
            rows.Add(string.Format(CultureInfo.InvariantCulture, "| {0} | {1:0.000} | {2:0.000} | {3:0.000} | {4} | {5} |", arm,
                P(.5) * 1000, P(.95) * 1000, sorted[^1] * 1000, bytes / Samples, sorted.Count(x => x >= 30)));
            return bytes;
        }

        bool off = false;
        long update = Run("Registry update (one re-check transition, then Start and DisabledScopes)", () =>
        {
            off = !off;
            registry.Recheck(CapabilityId.ResearchBilling, CapabilityRecheck.CityLoad, off ? CapabilityState.Off : CapabilityState.Active, "billing contract failed");
            _ = registry.Start(); _ = registry.DisabledScopes;
        });
        long fail = Run("Registry Fail on an Off capability (sticky no-op)", () => registry.Fail(CapabilityId.ResearchBilling, "again"));
        Run("Report Status, rebuilt (state changed since the last call)", () =>
        {
            off = !off;
            registry.Recheck(CapabilityId.ResearchBilling, CapabilityRecheck.CityLoad, off ? CapabilityState.Off : CapabilityState.Active, "billing contract failed");
            _ = report.Status(registry, "0.9.1");
        });
        long cached = Run("Report Status, reused (no change)", () => _ = report.Status(registry, "0.9.1"));
        Run("Report Show (food.dynamic, 23 hook lines)", () => _ = report.Show(registry, CapabilityId.FoodDynamic));

        var table = new List<string> { "| Arm | p50 µs | p95 µs | max µs | B/call | >= 30 ms |", "| --- | ---: | ---: | ---: | ---: | ---: |" };
        table.AddRange(rows);
        foreach (var line in table) output.WriteLine(line);
        string? path = PerfGate.Variable("PC_F8A_MEASURE");
        if (!string.IsNullOrEmpty(path)) File.AppendAllLines(path, table);

        Assert.Equal(0, update);
        Assert.Equal(0, fail);
        Assert.Equal(0, cached);
    }
}
