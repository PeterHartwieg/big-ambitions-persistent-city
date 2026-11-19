using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5OwnedMemoryTests
{
    private static string Source(params string[] path) => File.ReadAllText(Path.Combine(new[] { CapabilityReportTests.Root() }.Concat(path).ToArray()));

    [Fact]
    public void EveryDestroyedBodyReleasesItsMergedMeshTheWayTheNativeUndoDoes()
    {
        string pedestrians = Source("Runtime", "PhysicalPedestrians.cs");
        Assert.DoesNotContain("BodyMerge.Forget(", pedestrians);
        var destroys = Regex.Matches(pedestrians, @"UnityEngine\.Object\.Destroy\(character\.gameObject\);");
        Assert.Equal(2, destroys.Count);
        foreach (Match destroy in destroys)
        {
            int method = pedestrians.LastIndexOf("private ", destroy.Index, StringComparison.Ordinal);
            string body = pedestrians.Substring(method, destroy.Index - method);
            Assert.Contains("BodyMerge.Release(character.appearanceSetter", body);
        }
        string merge = Source("Runtime", "BodyMerge.cs");
        int release = merge.IndexOf("public static void Release(SkinnedMeshCombiner? combiner)", StringComparison.Ordinal);
        Assert.True(release > 0);
        string releaseBody = merge.Substring(release, merge.IndexOf("public static int LiveMeshes", release, StringComparison.Ordinal) - release);
        Assert.Contains("owned.Remove(combiner!);", releaseBody);
        Assert.Contains("combiner.resultMergeGameObject", releaseBody);
        Assert.Contains("Object.Destroy(mesh);", releaseBody);
        Assert.Contains("mergedMeshes.Remove(combiner!);\n            PruneMeshes();", releaseBody.Replace("\r", ""));
        int keyed = merge.IndexOf("mergedMeshes[combiner] = combined;", StringComparison.Ordinal);
        Assert.True(keyed > 0, "a redress replaces the combiner's previous mesh instead of adding another");
        Assert.True(keyed < merge.IndexOf("Fast++;", keyed, StringComparison.Ordinal), "only a finished merge is tracked, so Discard never leaves an entry");
        Assert.DoesNotContain("HashSet<Mesh>", merge);
        Assert.Contains("PruneMeshes();\n            if (hooked", merge.Replace("\r", ""));
    }

    [Fact]
    public void ASaveDuringTheStagedRebuildFinishesTheRebuildBeforeAnyMarketCompletion()
    {
        string mod = Source("Runtime", "PersistentCityMod.cs");
        int capture = mod.IndexOf("private SaveEpochCapture CaptureSteps(", StringComparison.Ordinal);
        int finish = mod.IndexOf("if (routeRebuild != null) FinishRouteRebuild(routeRebuild);", capture, StringComparison.Ordinal);
        Assert.True(finish > capture);
        Assert.True(finish < mod.IndexOf("BeginAccountingObservations()", capture, StringComparison.Ordinal));
        Assert.True(finish < mod.IndexOf("CompleteCampaignsForSave()", capture, StringComparison.Ordinal));
        int helper = mod.IndexOf("private void FinishRouteRebuild(RouteRebuild rebuild)", StringComparison.Ordinal);
        string body = mod.Substring(helper, mod.IndexOf("private void WaitForRetime()", helper, StringComparison.Ordinal) - helper);
        Assert.Contains("rebuild.Step(long.MaxValue)", body);
        Assert.Contains("vendorMarket?.Rebind(simulation);", body);
    }

    [Fact]
    public void GcDiagnosticsReportsTheModOwnedBodyMemory()
    {
        string movement = Source("Runtime", "MovementDiagnostics.cs");
        foreach (string field in new[] { "ownedBodyCombiners = BodyMerge.Owned", "unmergedBodyCombiners = BodyMerge.Unmerged", "reservedBodies = PhysicalPedestrians.ReserveCount", "liveMergedMeshes = meshes",
            "liveMergedVertices = vertices", "releasedMergedMeshes = BodyMerge.ReleasedMeshes", "releasedMergedVertices = BodyMerge.ReleasedVertices",
            "bakeScratchBytes = BlendshapeBake.ScratchBytes" })
            Assert.Contains(field, movement);
        int getter = movement.IndexOf("public object GcDiagnostics", StringComparison.Ordinal);
        Assert.Contains("BodyMerge.LiveMeshes(out long vertices)", movement.Substring(getter, 600));
    }
}
