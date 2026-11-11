using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Tests;

/// <summary>F8b (docs/plans/f8-capability-boundaries.md D7): `pc capability status | show` built from doubles of the
/// existing Runtime status members, and the source boundary of Runtime/CapabilityDiagnostics.cs.</summary>
[Collection(F8aSerial.Collection)]
public sealed class CapabilityReportTests
{
    private readonly ITestOutputHelper output;
    public CapabilityReportTests(ITestOutputHelper output) => this.output = output;
    internal static string Root([CallerFilePath] string path = "") => Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;

    /// <summary>Every input as a healthy running city reports it.</summary>
    internal static CapabilityInputs Healthy() => new CapabilityInputs
    {
        CityActive = true, FoodAuthorityInstalled = true, FoodMarketAttached = true, FoodPricePrefixInstalled = true, FoodDynamicActive = true,
        StreetFoodLoaded = true, VendorMarketAttached = true, VendorApiBound = true, VendorSnapshot = VendorApiStatus.Ready,
        ClockObserving = true, ClockCityActive = true, ClockFactor = 1.0 / 3, DrivenRidesLoaded = true, DrivenRidesCoordinated = true,
        RloReason = DonorReadReason.Ready, RloMessage = "Read 400 actual RLO citizens.",
        ResearchDialogInstalled = true, ResearchBillingInstalled = true, ResearchMessagesChecked = true, ResearchMessagesPassed = true,
        PhysicalEnabled = true, PhysicalActive = true,
        InspectorClickInstalled = true, InspectorScrollInstalled = true, InspectorUiBlockInstalled = true,
        InteriorConfigured = true, InteriorRequested = true, InteriorGuardAllowed = true, RoadSpeedsFromRlo = true, RoadSpeedsVerified = true,
        ReservationsChecked = true, ReservationsReady = true, ReservationsComplete = true,
    };

    /// <summary>Every capability the report reads (F8d added rlo.reservations and rlo.roadspeeds).</summary>
    private static readonly CapabilityId[] Read = NativeCapabilities.All.Select(d => d.Id).ToArray();

    public static IEnumerable<object[]> Cases()
    {
        object[] C(CapabilityId id, CapabilityState state, string? reason, Action<CapabilityInputs> change)
            => new object[] { id, state, reason ?? "", change };
        yield return C(CapabilityId.FoodAuthority, CapabilityState.Off, "GameManager.RunMainGameTick(Single) changed", i => i.FoodAuthorityRefusal = "GameManager.RunMainGameTick(Single) changed");
        yield return C(CapabilityId.FoodAuthority, CapabilityState.Off, "Food ownership suspended: x", i => i.FoodAuthorityError = "Food ownership suspended: x");
        yield return C(CapabilityId.FoodAuthority, CapabilityState.NotChecked, null, i => i.FoodAuthorityInstalled = false);
        yield return C(CapabilityId.FoodCheckoutShop, CapabilityState.Off, "ProcessCustomer changed", i => i.FoodCheckoutRefusal = "ProcessCustomer changed");
        yield return C(CapabilityId.FoodCheckoutShop, CapabilityState.Off, "price prefix is not installed", i => i.FoodPricePrefixInstalled = false);
        yield return C(CapabilityId.FoodCheckoutShop, CapabilityState.NotChecked, null, i => i.FoodMarketAttached = false);
        yield return C(CapabilityId.FoodDynamic, CapabilityState.Off, "capability failed: stamps", i => { i.FoodDynamicActive = false; i.FoodDynamicRefusal = "capability failed: stamps"; });
        yield return C(CapabilityId.FoodDynamic, CapabilityState.NotChecked, null, i => i.FoodDynamicActive = false);
        yield return C(CapabilityId.VendorStreetFood, CapabilityState.Absent, "is not loaded", i => i.StreetFoodLoaded = false);
        yield return C(CapabilityId.VendorStreetFood, CapabilityState.Off, "contract 2", i => { i.VendorApiBound = false; i.VendorReason = "contract 2"; });
        yield return C(CapabilityId.VendorStreetFood, CapabilityState.NotChecked, null, i => i.VendorMarketAttached = false);
        // Sol-r1-3: a bound API whose snapshot is not Ready suspends every vehicle scope; before the first read it is not checked.
        yield return C(CapabilityId.VendorStreetFood, CapabilityState.Off, "snapshot Off: Street Food is off", i => { i.VendorSnapshot = VendorApiStatus.Off; i.VendorReason = "Street Food is off"; });
        yield return C(CapabilityId.VendorStreetFood, CapabilityState.NotChecked, null, i => { i.VendorSnapshot = VendorApiStatus.NotYet; i.VendorReason = "Street Food has not been read yet."; });
        // Astra-r1-2: each RLO read reports its own result; Ready but different is off.
        yield return C(CapabilityId.RloDonor, CapabilityState.Off, "differs from the saved one", i => i.RloDiffers = true);
        yield return C(CapabilityId.RloRoadSpeeds, CapabilityState.Off, "Route preparation waits", i => i.RoadSpeedWaiting = "RLO road speed overrides: ReadSlowedLanes: RLO is refreshing the city's citizens.");
        yield return C(CapabilityId.RloRoadSpeeds, CapabilityState.NotChecked, null, i => i.RoadSpeedsFromRlo = false);
        yield return C(CapabilityId.RloRoadSpeeds, CapabilityState.NotChecked, null, i => i.RoadSpeedsVerified = false);
        yield return C(CapabilityId.RloRoadSpeeds, CapabilityState.Off, "ReadSlowedLanes: Multiplayer", i => { i.RoadSpeedsVerified = false; i.RoadSpeedRefusal = "RLO road speed overrides: ReadSlowedLanes: Multiplayer sessions are unsupported."; });
        yield return C(CapabilityId.RloRoadSpeeds, CapabilityState.Absent, "not loaded", i => i.RloReason = DonorReadReason.Missing);
        yield return C(CapabilityId.RloReservations, CapabilityState.Off, "ReadReservedBodies", i => { i.ReservationsReady = false; i.ReservationsError = "InvalidOperationException: ReadReservedBodies: RLO's CityApi reported a fault."; });
        yield return C(CapabilityId.RloReservations, CapabilityState.Off, "visible bodies pause", i => i.ReservationsComplete = false);
        yield return C(CapabilityId.RloReservations, CapabilityState.NotChecked, null, i => i.ReservationsChecked = false);
        // An unrelated physical failure no longer marks the reservations off.
        yield return C(CapabilityId.RloReservations, CapabilityState.Active, null, i => { i.PhysicalActive = false; i.PhysicalFailed = true; });
        yield return C(CapabilityId.RloReservations, CapabilityState.Absent, "not loaded", i => i.RloReason = DonorReadReason.Missing);
        yield return C(CapabilityId.ClockObserve, CapabilityState.Off, "observation unavailable", i => { i.ClockObserving = false; i.ClockObserveError = "Native clock observation unavailable"; });
        yield return C(CapabilityId.ClockObserve, CapabilityState.NotChecked, null, i => i.ClockObserving = false);
        yield return C(CapabilityId.ClockCity, CapabilityState.Off, "the runtime stopped (City clock patch ID already owned.)", i => { i.ClockCityActive = false; i.CityActive = false; i.CityFailed = true; i.FailureStatus = "City clock patch ID already owned."; });
        yield return C(CapabilityId.ClockCity, CapabilityState.NotChecked, null, i => i.ClockCityActive = false);
        yield return C(CapabilityId.ClockDrivenRides, CapabilityState.Absent, "Driven Rides is not loaded", i => i.DrivenRidesLoaded = false);
        yield return C(CapabilityId.ClockDrivenRides, CapabilityState.Active, null, i => { i.DrivenRidesCoordinated = false; i.ClockFactor = 1; });
        yield return C(CapabilityId.ClockDrivenRides, CapabilityState.NotChecked, null, i => i.DrivenRidesCoordinated = false);
        yield return C(CapabilityId.RloDonor, CapabilityState.Absent, "RealLifeOverhaul is required", i => { i.RloReason = DonorReadReason.Missing; i.RloMessage = "RealLifeOverhaul is required and is not loaded."; });
        yield return C(CapabilityId.RloDonor, CapabilityState.Off, "Multiplayer sessions", i => { i.RloReason = DonorReadReason.Shared; i.RloMessage = "Multiplayer sessions are unsupported."; });
        yield return C(CapabilityId.RloDonor, CapabilityState.NotChecked, null, i => i.RloReason = DonorReadReason.NotRead);
        yield return C(CapabilityId.ResearchDialog, CapabilityState.Off, "CampaignSettings changed", i => { i.ResearchDialogInstalled = false; i.ResearchDialogStatus = "MarketingAgencyDialog.CampaignSettings changed."; });
        yield return C(CapabilityId.ResearchDialog, CapabilityState.NotChecked, null, i => { i.ResearchDialogInstalled = false; i.ResearchDialogStatus = "Research dialog is not installed."; });
        yield return C(CapabilityId.ResearchBilling, CapabilityState.Off, "foreign Harmony patch", i => i.ResearchBillingStatus = "foreign Harmony patch on MarketingHelper.RunDaily by other.mod.");
        yield return C(CapabilityId.ResearchDelivery, CapabilityState.Off, "Contact.SendMessage changed", i => { i.ResearchMessagesPassed = false; i.ResearchMessagesStatus = "Contact.SendMessage changed."; });
        yield return C(CapabilityId.ResearchDelivery, CapabilityState.NotChecked, null, i => { i.ResearchMessagesPassed = false; i.ResearchMessagesChecked = false; });
        yield return C(CapabilityId.PhysicalVehicles, CapabilityState.Off, "stopped this session", i => { i.PhysicalActive = false; i.PhysicalFailed = true; });
        yield return C(CapabilityId.PhysicalPedestrians, CapabilityState.Off, "disabled in settings", i => { i.PhysicalActive = false; i.PhysicalEnabled = false; });
        yield return C(CapabilityId.PhysicalPedestrians, CapabilityState.NotChecked, null, i => i.PhysicalActive = false);
        yield return C(CapabilityId.InspectorClick, CapabilityState.Off, "inspector stopped", i => i.InspectorFailed = true);
        yield return C(CapabilityId.InspectorScroll, CapabilityState.Off, "scroll guard not installed", i => i.InspectorScrollInstalled = false);
        yield return C(CapabilityId.InspectorUiBlock, CapabilityState.Off, "UI block not installed", i => i.InspectorUiBlockInstalled = false);
        yield return C(CapabilityId.InspectorClick, CapabilityState.NotChecked, null, i => i.InspectorClickInstalled = i.InspectorScrollInstalled = i.InspectorUiBlockInstalled = false);
        // Sol-r1-5: NativeInteriorCityView.Start leaves Configured false for each of these; once a city runs each has its reason.
        yield return C(CapabilityId.InteriorView, CapabilityState.Off, "third-party interior mod", i => { i.InteriorConfigured = false; i.InteriorGuardAllowed = false; i.InteriorGuardReason = "third-party interior mod detected"; });
        yield return C(CapabilityId.InteriorView, CapabilityState.Off, "off in the settings (default)", i => { i.InteriorConfigured = false; i.InteriorRequested = false; });
        yield return C(CapabilityId.InteriorView, CapabilityState.Off, "native speed accessor unavailable", i => i.InteriorConfigured = false);
        yield return C(CapabilityId.InteriorView, CapabilityState.NotChecked, null, i => { i.InteriorConfigured = false; i.CityActive = false; });
    }

    [Fact]
    public void HealthyInputsReportEveryReadCapabilityActive()
    {
        var registry = CapabilitySnapshot.Build(Healthy());
        foreach (var id in Read) Assert.True(registry.State(id) == CapabilityState.Active, NativeCapabilities.Get(id).Name);
        Assert.Equal(CapabilityStartStatus.Ready, registry.Start().Status);
        string text = new CapabilityReport().Status(registry, "0.9.1");
        Assert.StartsWith("Persistent City capabilities (game 0.9.1): start ready\n", text);
        Assert.DoesNotContain("compatible", text, StringComparison.OrdinalIgnoreCase);
        // Before any city: required checks have not run, so the start waits; nothing claims active.
        var before = CapabilitySnapshot.Build(new CapabilityInputs { StreetFoodLoaded = true, DrivenRidesLoaded = true });
        Assert.Equal(CapabilityStartStatus.Waiting, before.Start().Status);
        Assert.All(NativeCapabilities.All, d => Assert.NotEqual(CapabilityState.Active, before.State(d.Id)));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ReportFromDoublesForEveryState(CapabilityId id, CapabilityState state, string reason, Action<CapabilityInputs> change)
    {
        var input = Healthy();
        change(input);
        var registry = CapabilitySnapshot.Build(input);
        Assert.Equal(state, registry.State(id));
        if (reason.Length > 0) Assert.Contains(reason, registry.Reason(id));
        // Optional and absent capabilities never block the start; a required Off is a capability refusal.
        var definition = NativeCapabilities.Get(id);
        var start = registry.Start();
        bool blocks = state == CapabilityState.Off && definition.Requirement != CapabilityRequirement.Optional
            || state == CapabilityState.Absent && definition.Requirement == CapabilityRequirement.Required;
        if (blocks) { Assert.Equal(CapabilityStartStatus.Refused, start.Status); Assert.Contains("not a damaged save", start.Message); }
        else if (definition.Requirement == CapabilityRequirement.Optional) Assert.NotEqual((CapabilityId?)id, start.Status == CapabilityStartStatus.Refused ? start.Capability : null);
        string line = new CapabilityReport().Status(registry, "x").Split('\n').Single(l => l.StartsWith(definition.Name + " ", StringComparison.Ordinal));
        Assert.Contains(" " + CapabilityReport.StateWord(state), line);
        string show = new CapabilityReport().Show(registry, id);
        Assert.StartsWith(line + "\nnative contract: ", show);
        Assert.Contains("\nfallback: " + definition.Fallback, show);
    }

    [Fact]
    public void EveryReadCapabilityHasACaseForEachState()
    {
        var covered = Cases().Select(c => ((CapabilityId)c[0], (CapabilityState)c[1])).ToHashSet();
        // Healthy() covers Active. Billing, scroll and UI block share their not-checked inputs with dialog and click.
        var sharedNotChecked = new[] { CapabilityId.ResearchBilling, CapabilityId.InspectorScroll, CapabilityId.InspectorUiBlock, CapabilityId.PhysicalVehicles };
        var companions = new[] { CapabilityId.VendorStreetFood, CapabilityId.ClockDrivenRides, CapabilityId.RloDonor, CapabilityId.RloReservations, CapabilityId.RloRoadSpeeds };
        foreach (var id in Read)
        {
            string name = NativeCapabilities.Get(id).Name;
            Assert.True(covered.Contains((id, CapabilityState.Off)) || id == CapabilityId.ClockDrivenRides, name + " off");
            Assert.True(covered.Contains((id, CapabilityState.NotChecked)) || sharedNotChecked.Contains(id), name + " not checked");
            if (companions.Contains(id)) Assert.True(covered.Contains((id, CapabilityState.Absent)), name + " absent");
        }
        // Driven Rides changed stops the city inside NativeCityClock.Start; the report shows the stop as clock.city off.
        var stopped = Healthy(); stopped.CityActive = stopped.ClockCityActive = false; stopped.CityFailed = true; stopped.FailureStatus = "Unsupported Driven Rides clock contract.";
        var registry = CapabilitySnapshot.Build(stopped);
        Assert.Equal(CapabilityState.Off, registry.State(CapabilityId.ClockDrivenRides));
        Assert.Contains("Unsupported Driven Rides clock contract.", registry.Reason(CapabilityId.ClockDrivenRides));
    }

    // ---- Source boundary of Runtime/CapabilityDiagnostics.cs -------------------------------------------------------

    /// <summary>Members that install, attach, persist, observe, sell, stop or write. The diagnostics file reads only.</summary>
    internal static readonly string[] Mutating =
    {
        ".Patch(", ".Unpatch", "UnpatchAll", "modData[", "modData.Remove", "modData.Add", "Persist(", ".Observe(", "Retire(", "Fail(", "Say(",
        ".Install(", ".Attach(", "Detach(", ".Dispose(", ".Write(", "WriteTo(", ".Release", "Withdraw(", "Begin(", "Confirm(", ".Buy(",
        "Recheck(", "StartObservation(", "NativeCityClock.Start(", "ReadRlo(", "rlo.Read(", "ChangeMoney", "SetActive(", "Destroy(",
    };

    /// <summary>The diagnostics files: F8b's report (later F8e/F8f branches) and F8e's Harmony metadata scan.</summary>
    internal static readonly string[] DiagnosticFiles = { "Runtime/CapabilityDiagnostics.cs", "Runtime/HookConflicts.cs" };

    [Fact]
    public void DiagnosticsFileCallsNoMutatingMember()
    {
        foreach (string file in DiagnosticFiles)
        {
            string text = StripComments(File.ReadAllText(Path.Combine(Root(), file)));
            foreach (string member in Mutating) Assert.False(text.Contains(member, StringComparison.Ordinal), file + " calls " + member);
        }
        string source = StripComments(File.ReadAllText(Path.Combine(Root(), "Runtime", "CapabilityDiagnostics.cs")));
        // Red with one injected mutating call.
        Assert.Contains(Mutating, m => (source + " NativeFoodOwnership.Observe(reg, false);").Contains(m, StringComparison.Ordinal));
    }

    [Fact]
    public void DiagnosticsAreNotReachedFromUpdate()
    {
        var violations = DiagnosticCallers(RuntimeSources());
        Assert.True(violations.Count == 0, string.Join("\n", violations));
        // Red with one injected per-frame call.
        var injected = RuntimeSources();
        injected["Runtime/PersistentCityMod.cs"] = injected["Runtime/PersistentCityMod.cs"].Replace(
            "if (!BeginAccountingObservations()) return;\n                foodMarket?.RefreshDiscovery();",
            "if (!BeginAccountingObservations()) return;\n                CapabilityCommand(\"status\", \"\");\n                foodMarket?.RefreshDiscovery();");
        Assert.NotEqual(RuntimeSources()["Runtime/PersistentCityMod.cs"], injected["Runtime/PersistentCityMod.cs"]);
        Assert.Contains(DiagnosticCallers(injected), v => v.Contains("UpdateCity", StringComparison.Ordinal));
        // The same through the qualified F8e scan.
        injected = RuntimeSources();
        injected["Runtime/PersistentCityMod.cs"] = injected["Runtime/PersistentCityMod.cs"].Replace(
            "foodMarket?.RefreshDiscovery();", "foodMarket?.RefreshDiscovery(); _ = HookConflicts.Scan();");
        Assert.Contains(DiagnosticCallers(injected), v => v.Contains("Scan from UpdateCity", StringComparison.Ordinal));
    }

    /// <summary>The only callers allowed outside the file: the console router, the once-per-load activation tail and F8h's
    /// once-per-load checkout decision (NativeFoodContract.CheckoutForLoad, called only from the market's constructor:
    /// HookConflictReportTests.TheRefusalIsDecidedOncePerLoadFromTheLoadScan).</summary>
    internal static readonly string[] AllowedCallers = { "RunCensusCommand", "CompleteInitialization", "CheckoutForLoad" };

    internal static Dictionary<string, string> RuntimeSources()
        => Directory.GetFiles(Path.Combine(Root(), "Runtime"), "*.cs").ToDictionary(
            f => "Runtime/" + Path.GetFileName(f), f => File.ReadAllText(f).Replace("\r\n", "\n"), StringComparer.Ordinal);

    private static readonly Regex MethodDeclaration = new(@"^\s*(?:(?:public|private|internal|protected|static|sealed|override|async|readonly|unsafe|extern)\s+)+[\w<>\[\]?,. ]+?\s+(?<name>\w+)\s*\([^;]*$", RegexOptions.Multiline);

    internal static List<string> DiagnosticCallers(Dictionary<string, string> sources)
    {
        string own = StripComments(sources["Runtime/CapabilityDiagnostics.cs"]);
        // Every member of the PopulationRuntime partial (private ones are visible to the other partials), and the
        // non-private members of HookConflicts, which other files can only reach as HookConflicts.Name(.
        var names = MethodDeclaration.Matches(own).Select(m => m.Groups["name"].Value)
            .Concat(MethodDeclaration.Matches(StripComments(sources["Runtime/HookConflicts.cs"])).Where(m => !m.Value.TrimStart().StartsWith("private", StringComparison.Ordinal))
                .Select(m => m.Groups["name"].Value))
            .Distinct().ToArray();
        Assert.Contains("CapabilityCommand", names);
        Assert.Contains("Scan", names);
        var violations = new List<string>();
        foreach (var pair in sources)
        {
            if (DiagnosticFiles.Contains(pair.Key)) continue;
            var lines = StripComments(pair.Value).Split('\n');
            string enclosing = "";
            for (int i = 0; i < lines.Length; i++)
            {
                var declared = MethodDeclaration.Match(lines[i]);
                if (declared.Success) enclosing = declared.Groups["name"].Value;
                foreach (string name in names)
                    if (Regex.IsMatch(lines[i], @"(?:(?<![\w.])|(?<=\bHookConflicts\.))" + name + @"\s*\(") && !(declared.Success && declared.Groups["name"].Value == name)
                        && !AllowedCallers.Contains(enclosing))
                        violations.Add(pair.Key + ":" + (i + 1) + " " + name + " from " + enclosing);
            }
        }
        return violations;
    }

    internal static string StripComments(string source)
        => Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\n]*", "");

    // ---- Wire and cost ------------------------------------------------------------------------------------------

    /// <summary>F8b-F8g change no wire. X3 owns the canonical golden pins of this fixture; this test only shows that the
    /// payload carries the current writer's envelope and that building and printing the report leaves it unchanged.</summary>
    [Theory]
    [InlineData(5150)]
    [InlineData(9217)]
    public void CurrentWriterGoldenHashIsUnchanged(int seed)
    {
        var state = ChangeJournalTests.Fixture(seed);
        string payload = SaveCodec.Write(state);
        Assert.StartsWith("{\"Schema\":" + CityState.CurrentSchema.ToString(CultureInfo.InvariantCulture) + ",", payload);
        // Building and printing the report reads no city state.
        _ = new CapabilityReport().Status(CapabilitySnapshot.Build(Healthy()), "x");
        _ = HookConflictReport.Build(Array.Empty<HookPatchedMethod>()).Text();
        Assert.Equal(payload, SaveCodec.Write(state));
    }

    /// <summary>Plan section 6, F8b: command cost (snapshot build plus report text) while the 957x2 economy city is
    /// alive. The command reads no population, so the city size does not enter the cost; the fixture shows it.</summary>
    [Fact]
    public void CommandCostOnThe957x2City()
    {
        var (state, _) = EconomyInterventions.EconomyFixture.Build(23, 957);
        Assert.Equal(957, state.Citizens.Count);
        const int Warm = 500, Samples = 5_000;
        var times = new double[Samples];
        double tick = 1000.0 / Stopwatch.Frequency;
        var input = Healthy(); input.RloReason = DonorReadReason.Off; input.RloMessage = "Flare is off";
        var report = new CapabilityReport();
        string status = "";
        for (int i = 0; i < Warm; i++) status = report.Status(CapabilitySnapshot.Build(input), "0.9.1");
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Samples; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            status = report.Status(CapabilitySnapshot.Build(input), "0.9.1");
            times[i] = (Stopwatch.GetTimestamp() - t0) * tick;
        }
        long bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / Samples;
        Array.Sort(times);
        string line = string.Format(CultureInfo.InvariantCulture, "F8b pc capability status: p50={0:0.000} ms p95={1:0.000} ms max={2:0.000} ms bytes/call={3} residents={4}",
            times[Samples / 2], times[Samples * 95 / 100], times[^1], bytes, state.Citizens.Count);
        output.WriteLine(line);
        string? path = Environment.GetEnvironmentVariable("PC_F8_MEASURE");
        if (!string.IsNullOrEmpty(path)) File.AppendAllLines(path, new[] { line });
        Assert.Contains("rlo.donor required off: transient (Off): Flare is off", status);
        PerfGate.Under(times[Samples * 95 / 100], 5, "F8b command p95");
    }
}
