using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class SaveCompressionPatchTests
{
    private static readonly SaveCompressionPatch Own = SaveCompressionPatches.HandOffPrefix;
    private static readonly SaveCompressionPatch StreetFood = SaveCompressionPatches.StreetFoodBridgeObserver;

    private static SaveCompressionPatchCheck Check(params SaveCompressionPatch[] patches) =>
        SaveCompressionPatches.Check(patches, patches.Select(p => p.Owner).Distinct());

    private static SaveCompressionPatch Like(SaveCompressionPatch shape, string? owner = null, HookPatchKind? kind = null,
        string? type = null, string? method = null, string? returns = null, string[]? parameters = null) =>
        new SaveCompressionPatch(owner ?? shape.Owner, kind ?? shape.Kind, type ?? shape.DeclaringType, method ?? shape.Method,
            returns ?? shape.ReturnType, parameters ?? shape.Parameters.ToArray());

    [Fact]
    public void OnlyTheHandOffPrefixIsCompatible()
    {
        var check = Check(Own);
        Assert.Null(check.Conflict);
        Assert.Empty(check.Accepted);
    }

    [Fact]
    public void TheStreetFoodBridgeObserverIsAcceptedAndReported()
    {
        var check = Check(Own, StreetFood);
        Assert.Null(check.Conflict);
        Assert.Equal(new[] { "foodtrucks.mod finalizer FoodTrucks.TestBridgeCommands.CompressFinalizer(System.String path, System.String outputPath, System.Boolean __result, System.Exception __exception)" },
            check.Accepted);
    }

    [Fact]
    public void UnknownForeignCompressionPatchesStillRefuseTheBackgroundPath()
    {
        var refused = new[]
        {
            new SaveCompressionPatch("other.mod", HookPatchKind.Prefix, "Other.Patches", "CompressPrefix", "System.Boolean", "System.String path", "System.String outputPath"),
            new SaveCompressionPatch("other.mod", HookPatchKind.Postfix, "Other.Patches", "CompressPostfix", "System.Void", "System.String outputPath"),
            new SaveCompressionPatch("other.mod", HookPatchKind.Transpiler, "Other.Patches", "Rewrite", "System.Collections.Generic.IEnumerable`1", "instructions"),
            Like(StreetFood, owner: "other.mod"),
            Like(StreetFood, kind: HookPatchKind.Prefix),
            Like(StreetFood, kind: HookPatchKind.Postfix),
            Like(StreetFood, type: "FoodTrucks.Patches"),
            Like(StreetFood, method: "CompressPostfix"),
            Like(StreetFood, returns: "System.Void"),
            Like(StreetFood, parameters: new[] { "System.String path", "System.String outputPath", "System.Boolean& __result", "System.Exception __exception" }),
            Like(StreetFood, parameters: new[] { "System.String path", "System.String outputPath", "System.Boolean __result" }),
        };
        foreach (var patch in refused)
        {
            var check = Check(Own, patch);
            Assert.NotNull(check.Conflict);
            Assert.StartsWith(SaveCompressionPatches.ConflictPrefix + patch.Owner + " (", check.Conflict);
            Assert.Contains(patch.Describe(), check.Conflict);
            var withStreetFood = Check(Own, StreetFood, patch);
            Assert.NotNull(withStreetFood.Conflict);
        }
    }

    [Fact]
    public void AnOwnerWhosePatchKindCannotBeInspectedRefuses()
    {
        var check = SaveCompressionPatches.Check(new[] { Own, StreetFood }, new[] { Own.Owner, StreetFood.Owner, "hidden.mod" });
        Assert.Equal(SaveCompressionPatches.ConflictPrefix + "hidden.mod (a patch kind Persistent City cannot inspect).", check.Conflict);
    }

    [Fact]
    public void AMissingOrChangedHandOffPrefixRefuses()
    {
        Assert.Equal("Save hand-off patch is missing on CompressBinaryData.", Check(StreetFood).Conflict);
        Assert.Equal("Save hand-off patch is missing on CompressBinaryData.", Check().Conflict);
        Assert.StartsWith("The save hand-off owner has an unexpected patch", Check(Own, Like(Own, kind: HookPatchKind.Postfix)).Conflict);
        Assert.NotNull(Check(Own, Own).Conflict);
    }

    [Fact]
    public void TheHandOffPrefixShapeMatchesTheRuntimeDeclaration()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "Runtime", "NativeSaveHandOff.cs"));
        Assert.Contains("namespace PersistentCity.Runtime", source);
        Assert.Contains("internal static class NativeSaveHandOff", source);
        Assert.Contains("private static bool CompressPrefix(string path, string outputPath, ref bool __result)", source);
        Assert.Equal("PersistentCity.Runtime.NativeSaveHandOff", Own.DeclaringType);
        Assert.Equal(new[] { "System.String path", "System.String outputPath", "System.Boolean& __result" }, Own.Parameters);
        Assert.Contains("SaveCompressionPatches.Check(patches, info.Owners)", source);
    }

    private static string Root([CallerFilePath] string path = "") => Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;
}
