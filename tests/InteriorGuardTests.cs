using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;
public sealed class InteriorGuardTests
{
    private static InteriorGuardVerdict Evaluate(string[] names, string[]? owners = null, string[]? rules = null)
        => ThirdPartyInteriorGuard.Evaluate("auto", new[] { "TrueCityInteriors", "TrueCity*" },
            rules ?? Array.Empty<string>(), names, owners ?? Array.Empty<string>());

    [Theory]
    [InlineData("TrueCityInteriors")] [InlineData("truecityinteriors")]
    [InlineData("TRUECITY.StandIn")] [InlineData("TrueCity")]
    public void DefaultAssemblyRulesDetect(string name) => Assert.True(Evaluate(new[] { name }).Detected);

    [Theory]
    [InlineData("PersistentCity")] [InlineData("PersistentCity.Core")] [InlineData("0Harmony")]
    [InlineData("RealLifeOverhaul")] [InlineData("System.Core")]
    [InlineData("AlmostTrueCityInteriors")] [InlineData("TrueCitiesInteriors")]
    public void SimilarAndCommonAssembliesStayClear(string name) => Assert.False(Evaluate(new[] { name }).Detected);

    [Fact]
    public void ExactRuleDoesNotMatchSuffixAndOwnersAreExactOrdinalIds()
    {
        Assert.False(ThirdPartyInteriorGuard.Evaluate("auto", new[] { "Example" }, Array.Empty<string>(),
            new[] { "Example.More" }, Array.Empty<string>()).Detected);
        Assert.True(Evaluate(Array.Empty<string>(), new[] { "our.harmless.fixture" }, new[] { "our.harmless.fixture" }).Detected);
        Assert.False(Evaluate(Array.Empty<string>(), new[] { "OUR.harmless.fixture" }, new[] { "our.harmless.fixture" }).Detected);
        Assert.False(Evaluate(Array.Empty<string>(), new[] { "our.harmless.fixture" }).Detected);
    }

    private static IEnumerable<string> Broken()
        => new ThrowingNames();
    private sealed class ThrowingNames : IEnumerable<string>
    {
        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("test");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void ErrorsFailSafeButOffAndEmptyOwnerRulesNeverEnumerateUnusedInputs()
    {
        var patterns = new[] { "TrueCity*" };
        Assert.True(ThirdPartyInteriorGuard.Evaluate("auto", patterns, Array.Empty<string>(), Broken(), Broken()).Detected);
        Assert.True(ThirdPartyInteriorGuard.Evaluate("auto", patterns, new[] { "fixture" }, Array.Empty<string>(), Broken()).Detected);
        Assert.False(ThirdPartyInteriorGuard.Evaluate("auto", patterns, Array.Empty<string>(), Array.Empty<string>(), Broken()).Detected);
        var off = ThirdPartyInteriorGuard.Evaluate("off", null, null, Broken(), Broken());
        Assert.False(off.Detected); Assert.Equal("guard off", off.Reason);
    }

    [Theory]
    [InlineData("null")] [InlineData("{}")] [InlineData("[null]")] [InlineData("[1]")]
    [InlineData("[\"*\"]")] [InlineData("[\"True*City\"]")] [InlineData("[\"\"]")]
    public void InvalidRulesRefuseWithoutDiscardingOtherSettings(string input)
    {
        var s = JsonConvert.DeserializeObject<PrototypeSettings>("{\"Enabled\":false,\"ThirdPartyInteriorAssemblyPatterns\":" + input + "}")!;
        Assert.False(s.Enabled);
        Assert.True(ThirdPartyInteriorGuard.Evaluate(s.ThirdPartyInteriorGuard, s.ThirdPartyInteriorAssemblyPatterns,
            s.ThirdPartyInteriorHarmonyOwnerIds, Array.Empty<string>(), Array.Empty<string>()).Detected);
    }

    [Theory]
    [InlineData("null")] [InlineData("true")] [InlineData("\"AUTO\"")] [InlineData("\"bad\"")]
    public void InvalidModeFailsSafe(string input)
    {
        var s = JsonConvert.DeserializeObject<PrototypeSettings>("{\"ThirdPartyInteriorGuard\":" + input + "}")!;
        Assert.True(ThirdPartyInteriorGuard.Evaluate(s.ThirdPartyInteriorGuard, s.ThirdPartyInteriorAssemblyPatterns,
            s.ThirdPartyInteriorHarmonyOwnerIds, Array.Empty<string>(), Array.Empty<string>()).Detected);
    }

    [Fact]
    public void DetectedForcesAnAlreadyActiveF0ViewOffEvenWhenRequestedOn()
    {
        var s = new PrototypeSettings { InteriorCityView = "on" };
        Assert.Equal("auto", s.ThirdPartyInteriorGuard);
        Assert.Empty(s.ThirdPartyInteriorHarmonyOwnerIds!);
        var state = new InteriorViewState();
        var travel = new TravelVisibility(false, false, false, true, false, false);
        state.Refresh(true, travel, this, 0, () => InteriorViewEligibility.Check(true, false, default, 0));
        Assert.True(state.Active);
        var verdict = Evaluate(new[] { "TrueCity.OurTestOnly" });
        state.Refresh(verdict.AllowsView(s.InteriorCityView == "on"), travel, this, 3,
            () => throw new Exception("blocked view must not inspect"));
        Assert.False(state.Active);
        Assert.False(verdict.AllowsView(true));
        Assert.True(Evaluate(new[] { "PersistentCity" }).AllowsView(true));
    }

    [Fact]
    public void DetectionIsWiredOnlyToCompletedLoadAndDiagnosticsReadOnlyCachedState()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        string Read(string path) => File.ReadAllText(Path.Combine(root!.FullName, path));
        var adapter = Read("Runtime/NativeInteriorCityView.cs");
        var start = adapter.Substring(adapter.IndexOf("public static void Start("), adapter.IndexOf("private static void OnSceneUnloaded") - adapter.IndexOf("public static void Start("));
        Assert.Contains("NativeInteriorGuard.Start(loaded);", start);
        Assert.Contains("Configured = NativeInteriorGuard.Verdict.AllowsView(loaded.InteriorCityView == \"on\");", start);
        Assert.Equal(2, adapter.Split("NativeInteriorGuard.Start(").Length);
        var mod = Read("Runtime/PersistentCityMod.cs");
        var load = mod.Substring(mod.IndexOf("private void CompleteInitialization()"), mod.IndexOf("private static double Now()") - mod.IndexOf("private void CompleteInitialization()"));
        Assert.Contains("NativeInteriorCityView.Start(settings, log!, RestoreInterior);", load);
        Assert.Equal(2, mod.Split("NativeInteriorCityView.Start(").Length);
        var dump = Read("Runtime/InteriorGuardDiagnostics.cs");
        Assert.DoesNotContain(".Start(", dump); Assert.DoesNotContain("NativeInteriorCityView.Active", dump);
        Assert.Contains("NativeInteriorCityView.State.Active", dump);
        Assert.DoesNotContain("GetAssemblies", adapter);
    }
}
