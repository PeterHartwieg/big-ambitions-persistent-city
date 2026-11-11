using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F8a (docs/plans/f8-capability-boundaries.md D8): the source inventory test. It reads the production sources
/// and fails when a file adds a Harmony owner or a patch target that <see cref="NativeHookInventory"/> does not declare,
/// or when a declared one is gone. The checker is a pure function over file contents so injected sources prove it red.</summary>
[Collection(F8aSerial.Collection)]
public sealed class HookInventorySourceTests
{
    private static string Root([CallerFilePath] string path = "") => Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;

    /// <summary>Non-Harmony <c>.Patch(</c> calls: the router calling its <see cref="IFoodContractPatcher"/>.</summary>
    private static readonly Dictionary<string, int> RouterPatchCalls = new(StringComparer.Ordinal) { ["Runtime/FoodContractHooks.cs"] = 1 };

    /// <summary>Harmony <c>.Patch(</c> sites whose target is a helper or table parameter, not a literal member. Their targets
    /// are checked through the helper calls (<c>Patch(typeof(T), "M", ...)</c>) or the food-contract table instead.</summary>
    private static readonly Dictionary<string, int> IndirectPatchSites = new(StringComparer.Ordinal)
    {
        ["Runtime/NativeFoodOwnership.cs"] = 1, ["Runtime/PhysicalTrafficHooks.cs"] = 1, ["Runtime/LoadedCatalogueLifecycleMembers.cs"] = 1,
    };

    private static readonly Regex NewHarmony = new(@"new\s+Harmony\(\s*(?:""(?<literal>[^""]*)""|(?<name>[A-Za-z_]\w*))\s*\)");
    private static readonly Regex PatchCall = new(@"\.Patch\w*\(");
    private static readonly Regex HelperTarget = new(@"(?<![\w.])Patch\(\s*typeof\((?<type>[\w.]+)\)\s*,\s*""(?<method>\w+)""");
    private static readonly Regex TableRow = new(@"new\s+FoodContractHook\(\s*\d+\s*,\s*""\w+""\s*,\s*""(?<type>\w+)""\s*,\s*""(?<method>\w+)""\s*,"
        + @"\s*(?:true|false)\s*,\s*FoodContractHookKind\.\w+(?<parameters>(?:\s*,\s*""[^""]*"")*)\s*\)");
    private static readonly Regex MemberLookup = new(@"(?:typeof\((?<type>[\w.]+)\)\s*\.\s*GetMethod\(\s*|AccessTools\.Method\(\s*(?:typeof\((?<type>[\w.]+)\)|\w+)\s*,\s*)"
        + @"(?:""(?<method>\w+)""|nameof\((?<named>[\w.]+)\))");
    private static readonly Regex AttributePatching = new(@"\[\s*(?:HarmonyLib\.)?HarmonyPatch\b|\bPatchAll\s*\(|\bCreateAndPatchAll\s*\(|\bPatchCategory\s*\(|\bCreateClassProcessor\s*\(");
    private static readonly Regex Quoted = new(@"""([^""]*)""");
    private static readonly Regex FullLineComment = new(@"^\s*//.*$", RegexOptions.Multiline);

    internal static Dictionary<string, string> Sources()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        string root = Root();
        foreach (var folder in new[] { "Core", "Persistence", "Compatibility", "Runtime" })
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.Contains("/obj/") || relative.Contains("/bin/")) continue;
                files[relative] = File.ReadAllText(path);
            }
        return files;
    }

    internal static List<(string Type, string Method, string[] Parameters)> TableTargets(string source)
        => TableRow.Matches(source).Select(m => (m.Groups["type"].Value, m.Groups["method"].Value,
            Quoted.Matches(m.Groups["parameters"].Value).Select(q => q.Groups[1].Value).ToArray())).ToList();

    /// <summary>Every mismatch between the sources and the inventory, in a stable order. Empty means they agree.</summary>
    internal static List<string> Check(IReadOnlyDictionary<string, string> files)
    {
        var findings = new List<string>();
        var declaredFiles = NativeHookInventory.Owners.ToDictionary(o => o.File, StringComparer.Ordinal);
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (file, raw) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            string source = FullLineComment.Replace(raw, "");
            if (AttributePatching.IsMatch(source)) findings.Add(file + ": attribute or bulk Harmony patching, which the inventory does not track");
            foreach (Match match in NewHarmony.Matches(source))
            {
                string? owner = match.Groups["literal"].Success ? match.Groups["literal"].Value : Constant(source, match.Groups["name"].Value);
                if (owner == null) { findings.Add(file + ": Harmony owner expression '" + match.Groups["name"].Value + "' is not a string constant"); continue; }
                var declared = NativeHookInventory.Owner(owner);
                if (declared == null) findings.Add(file + ": undeclared Harmony owner " + owner);
                else if (declared.File != file) findings.Add(file + ": Harmony owner " + owner + " is declared in " + declared.File);
                else found.Add(owner);
            }
            int calls = PatchCall.Matches(source).Count;
            if (calls == 0) continue;
            if (declaredFiles.TryGetValue(file, out var fileOwner))
            {
                if (calls != fileOwner.PatchCallSites)
                    findings.Add(file + ": " + calls + " .Patch( call sites, inventory declares " + fileOwner.PatchCallSites + " for " + fileOwner.Id);
                var targets = NativeHookInventory.Targets.Where(t => t.Owner == fileOwner.Id).ToList();
                foreach (Match helper in HelperTarget.Matches(source))
                {
                    string type = helper.Groups["type"].Value.Split('.').Last(), method = helper.Groups["method"].Value;
                    if (!targets.Any(t => t.Type == type && t.Method == method))
                        findings.Add(file + ": undeclared target " + type + "." + method + " for " + fileOwner.Id);
                }
                // Each direct site's first argument, resolved through its local or field assignment, must be a declared target.
                int indirect = 0;
                foreach (Match call in PatchCall.Matches(source))
                {
                    var target = DirectTarget(source, call.Index + call.Length);
                    if (target == null) { indirect++; continue; }
                    var (type, method) = target.Value;
                    if (!targets.Any(t => t.Method == method && (type == null || t.Type == type)))
                        findings.Add(file + ": undeclared target " + (type ?? "?") + "." + method + " for " + fileOwner.Id);
                }
                IndirectPatchSites.TryGetValue(file, out int allowed);
                if (indirect != allowed)
                    findings.Add(file + ": " + indirect + " .Patch( sites with an unresolved target, expected " + allowed);
            }
            else if (!RouterPatchCalls.TryGetValue(file, out int router) || router != calls)
                findings.Add(file + ": " + calls + " .Patch( call sites in a file with no declared Harmony owner");
        }
        foreach (var owner in NativeHookInventory.Owners)
        {
            if (!found.Contains(owner.Id)) findings.Add(owner.File + ": declared Harmony owner " + owner.Id + " not found");
            var targets = NativeHookInventory.Targets.Where(t => t.Owner == owner.Id).ToList();
            if (owner.TableFile != null)
            {
                var rows = files.TryGetValue(owner.TableFile, out var table) ? TableTargets(table) : new();
                var actual = rows.Select(r => r.Type + "." + r.Method + "(" + string.Join(", ", r.Parameters) + ")").ToList();
                var expected = targets.Select(t => t.Type + "." + t.Method + "(" + string.Join(", ", t.Parameters ?? Array.Empty<string>()) + ")").ToList();
                foreach (var extra in actual.Except(expected)) findings.Add(owner.TableFile + ": undeclared target " + extra + " for " + owner.Id);
                foreach (var gone in expected.Except(actual)) findings.Add(owner.TableFile + ": declared target " + gone + " not found for " + owner.Id);
                if (actual.Count != expected.Count) findings.Add(owner.TableFile + ": " + actual.Count + " table rows, inventory declares " + expected.Count);
                continue;
            }
            if (!files.TryGetValue(owner.File, out var text)) continue;
            text = FullLineComment.Replace(text, "");
            foreach (var target in targets)
                if (!Regex.IsMatch(text, @"""" + target.Method + @"""|nameof\([\w.]*\b" + target.Method + @"\)") || !Regex.IsMatch(text, @"\b" + target.Type + @"\b"))
                    findings.Add(owner.File + ": declared target " + target.Signature + " not found for " + owner.Id);
        }
        return findings;
    }

    /// <summary>The (type, method) a <c>.Patch(</c> call patches: its first argument, or the expression last assigned to
    /// that name before the call (else the first after it), counting only assignments that look a member up. Type is null when the source looks the type up at
    /// run time (Driven Rides). Null when the target is a helper or table parameter.</summary>
    private static (string? Type, string Method)? DirectTarget(string source, int start)
    {
        int depth = 0, end = start;
        for (; end < source.Length; end++)
        {
            char c = source[end];
            if (c == '(' || c == '[' || c == '{') depth++;
            else if (c == ')' || c == ']' || c == '}') { if (depth == 0) break; depth--; }
            else if (c == ',' && depth == 0) break;
        }
        string argument = source.Substring(start, end - start).Trim().TrimEnd('!');
        var lookup = MemberLookup.Match(argument);
        if (!lookup.Success && Regex.IsMatch(argument, @"^[A-Za-z_]\w*$"))
        {
            var assignments = Regex.Matches(source, @"(?<![\w.])" + Regex.Escape(argument) + @"\s*=(?![=>])\s*(?<expr>[^;]*);").Cast<Match>()
                .Where(a => MemberLookup.IsMatch(a.Groups["expr"].Value)).ToList();
            var chosen = assignments.LastOrDefault(a => a.Index < start) ?? assignments.FirstOrDefault();
            if (chosen != null) lookup = MemberLookup.Match(chosen.Groups["expr"].Value);
        }
        if (!lookup.Success) return null;
        string method = lookup.Groups["method"].Success ? lookup.Groups["method"].Value : lookup.Groups["named"].Value.Split('.').Last();
        return (lookup.Groups["type"].Success ? lookup.Groups["type"].Value.Split('.').Last() : null, method);
    }

    private static string? Constant(string source, string name)
    {
        var match = Regex.Match(source, @"\bconst\s+string\s+" + Regex.Escape(name) + @"\s*=\s*""(?<value>[^""]*)""");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static Dictionary<string, string> With(string file, Func<string, string> edit)
    {
        var files = Sources();
        files[file] = edit(files.TryGetValue(file, out var text) ? text : "");
        return files;
    }

    [Fact]
    public void ProductionSourcesMatchTheInventory()
    {
        var findings = Check(Sources());
        Assert.True(findings.Count == 0, string.Join("\n", findings));
    }

    [Fact]
    public void AnInjectedTestOnlyOwnerTurnsTheCheckRed()
    {
        const string injected = """
            using HarmonyLib;
            namespace PersistentCity.Runtime
            {
                internal sealed class InjectedTestOwner
                {
                    private const string Owner = "persistentcity.injected-test-owner";
                    private readonly Harmony harmony = new Harmony(Owner);
                    internal void Install(System.Reflection.MethodInfo target) => harmony.Patch(target, prefix: null);
                }
            }
            """;
        var findings = Check(With("Runtime/InjectedTestOwner.cs", _ => injected));
        Assert.Contains("Runtime/InjectedTestOwner.cs: undeclared Harmony owner persistentcity.injected-test-owner", findings);
        Assert.Contains("Runtime/InjectedTestOwner.cs: 1 .Patch( call sites in a file with no declared Harmony owner", findings);
        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void AnUndeclaredTargetOnADeclaredOwnerTurnsTheCheckRed()
    {
        // Through the food-authority helper: no new .Patch( call site, so only the target check can see it.
        var helper = Check(With("Runtime/NativeFoodOwnership.cs", s => s.Replace(
            "                installed = true;", "                Patch(typeof(RetailBusinessSimulator), \"InjectedTarget\", nameof(CustomerSale));\n                installed = true;")));
        Assert.Equal(new[] { "Runtime/NativeFoodOwnership.cs: undeclared target RetailBusinessSimulator.InjectedTarget for " + NativeHookInventory.FoodAuthority }, helper);

        // A direct extra patch on the city clock.
        var direct = Check(With("Runtime/NativeCityClock.cs", s => s.Replace("Factor = factor; save = currentSave;",
            "installing.Patch(tick, postfix: new HarmonyMethod(typeof(NativeCityClock), nameof(TickPrefix)));\n                Factor = factor; save = currentSave;")));
        Assert.Equal(new[] { "Runtime/NativeCityClock.cs: 4 .Patch( call sites, inventory declares 3 for " + NativeHookInventory.CityClock }, direct);

        // An existing direct site redirected to an undeclared target: same call count, every declared token still present.
        var redirected = Check(With("Runtime/NativeCityClock.cs", s => s.Replace("installing.Patch(tick, prefix: new HarmonyMethod(typeof(NativeCityClock), nameof(AccountingPrefix))",
            "installing.Patch(AccessTools.Method(typeof(GameManager), \"InjectedTarget\"), prefix: new HarmonyMethod(typeof(NativeCityClock), nameof(AccountingPrefix))")));
        Assert.Equal(new[] { "Runtime/NativeCityClock.cs: undeclared target GameManager.InjectedTarget for " + NativeHookInventory.CityClock }, redirected);

        // The same through a variable: the field assignment is resolved.
        var variable = Check(With("Runtime/StationaryCrowds.cs", s => s.Replace("AccessTools.Method(typeof(StationaryAiSpawner), \"Spawn\"",
            "AccessTools.Method(typeof(StationaryAiSpawner), \"InjectedSpawn\"")));
        Assert.Contains("Runtime/StationaryCrowds.cs: undeclared target StationaryAiSpawner.InjectedSpawn for " + NativeHookInventory.StationaryCrowds, variable);

        // A new row in the table-driven food-contract owner.
        var table = Check(With("Runtime/FoodContractHooks.cs", s => s.Replace("FoodContractHookKind.Unattributed),",
            "FoodContractHookKind.Unattributed), new FoodContractHook(23, \"W1\", \"ItemHelper\", \"InjectedTarget\", true, FoodContractHookKind.Quantity, \"Int32\"),")));
        Assert.Contains("Runtime/FoodContractHooks.cs: undeclared target ItemHelper.InjectedTarget(Int32) for " + NativeHookInventory.FoodContract, table);
    }

    [Fact]
    public void ARemovedOrMovedDeclarationTurnsTheCheckRed()
    {
        var gone = Check(With("Runtime/NativeCityClock.cs", s => s.Replace("\"CapTick\"", "\"CapTickRenamed\"")));
        Assert.Equal(new[] { "Runtime/NativeCityClock.cs: undeclared target ?.CapTickRenamed for " + NativeHookInventory.CityClock,
            "Runtime/NativeCityClock.cs: declared target Ride.CapTick(Single&) not found for " + NativeHookInventory.CityClock }, gone);

        var moved = Check(With("Runtime/StationaryCrowds.cs", s => s.Replace(NativeHookInventory.StationaryCrowds, "hart.persistentcity.renamed.v1")));
        Assert.Contains("Runtime/StationaryCrowds.cs: undeclared Harmony owner hart.persistentcity.renamed.v1", moved);
        Assert.Contains("Runtime/StationaryCrowds.cs: declared Harmony owner " + NativeHookInventory.StationaryCrowds + " not found", moved);
    }

    [Fact]
    public void AttributeOrBulkPatchingTurnsTheCheckRed()
    {
        var findings = Check(With("Runtime/InjectedAttributePatch.cs", _ =>
            "[HarmonyLib.HarmonyPatch(typeof(GameManager), \"RunMainGameTick\")] internal static class Injected { }\n"
            + "internal static class Installer { internal static void Run() => HarmonyLib.Harmony.CreateAndPatchAll(typeof(Injected)); }"));
        Assert.Equal(new[] { "Runtime/InjectedAttributePatch.cs: attribute or bulk Harmony patching, which the inventory does not track" }, findings);
    }

    /// <summary>The table parser reads the same rows the compiled table holds, so the table arm checks the real list.</summary>
    [Fact]
    public void TableParserAgreesWithTheCompiledFoodContractTable()
    {
        var rows = TableTargets(File.ReadAllText(Path.Combine(Root(), "Runtime", "FoodContractHooks.cs")));
        Assert.Equal(FoodContractHookTable.All.Select(h => h.Type + "." + h.Method + "(" + string.Join(", ", h.Parameters) + ")"),
            rows.Select(r => r.Type + "." + r.Method + "(" + string.Join(", ", r.Parameters) + ")"));
    }

    /// <summary>The counts at the F8a base (re-read after F2c-F2e, E5b2/E5b3 and F5h): 12 owners and 52 owner-target rows.
    /// The plan's section 2 counted 11 owners and 27 methods; E5b2 added food-contract with 23 hint postfixes. Every
    /// target belongs to exactly one capability, and every owner's targets to the capabilities D2 names for it.</summary>
    [Fact]
    public void InventoryCountsAndCapabilityMapping()
    {
        Assert.Equal(12, NativeHookInventory.Owners.Count);
        Assert.Equal(52, NativeHookInventory.Targets.Count);
        Assert.Equal(NativeHookInventory.Owners.Count, NativeHookInventory.Owners.Select(o => o.Id).Distinct().Count());
        Assert.All(NativeHookInventory.Targets, t => Assert.NotNull(NativeHookInventory.Owner(t.Owner)));
        Assert.All(NativeHookInventory.Owners, o => Assert.Contains(NativeHookInventory.Targets, t => t.Owner == o.Id));
        Assert.Equal(NativeHookInventory.Targets.Count, NativeHookInventory.Targets
            .Select(t => t.Owner + " " + t.Signature).Distinct().Count());
        Assert.All(NativeHookInventory.Targets, t => Assert.NotEqual(NativePatchKinds.None, t.Kinds));

        string Map(string owner) => string.Join(",", NativeHookInventory.Targets.Where(t => t.Owner == owner)
            .Select(t => NativeCapabilities.Get(t.Capability).Name).Distinct());
        Assert.Equal("food.authority", Map(NativeHookInventory.FoodAuthority));
        Assert.Equal("food.checkout.shop", Map(NativeHookInventory.FoodRoutines));
        Assert.Equal("food.dynamic", Map(NativeHookInventory.FoodContract));
        Assert.Equal("clock.city,clock.drivenrides", Map(NativeHookInventory.CityClock));
        Assert.Equal("clock.observe", Map(NativeHookInventory.ClockObserver));
        Assert.Equal("research.billing", Map(NativeHookInventory.ResearchBilling));
        Assert.Equal("research.dialog", Map(NativeHookInventory.ResearchDialog));
        Assert.Equal("physical.vehicles", Map(NativeHookInventory.PhysicalVehicles));
        Assert.Equal("physical.vehicles", Map(NativeHookInventory.PhysicalIntersections));
        Assert.Equal("physical.pedestrians", Map(NativeHookInventory.AmbientPedestrians));
        Assert.Equal("physical.pedestrians", Map(NativeHookInventory.StationaryCrowds));
        Assert.Equal("inspector.click,inspector.scroll,inspector.uiblock", Map(NativeHookInventory.InspectorInput));

        // GameManager.RunMainGameTick carries three PC owners (H1, H3, H4): normal, not a conflict (plan section 2).
        Assert.Equal(new[] { NativeHookInventory.FoodAuthority, NativeHookInventory.CityClock, NativeHookInventory.ClockObserver },
            NativeHookInventory.Targets.Where(t => t.Type == "GameManager" && t.Method == "RunMainGameTick").Select(t => t.Owner));
    }
}
