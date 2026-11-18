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
        Assert.Contains("mergedMeshes.Add(combined);", merge);
    }

    [Fact]
    public void GcDiagnosticsReportsTheModOwnedBodyMemory()
    {
        string movement = Source("Runtime", "MovementDiagnostics.cs");
        foreach (string field in new[] { "ownedBodyCombiners = BodyMerge.Owned", "reservedBodies = PhysicalPedestrians.ReserveCount", "liveMergedMeshes = meshes",
            "liveMergedVertices = vertices", "releasedMergedMeshes = BodyMerge.ReleasedMeshes", "releasedMergedVertices = BodyMerge.ReleasedVertices",
            "bakeScratchBytes = BlendshapeBake.ScratchBytes" })
            Assert.Contains(field, movement);
        int getter = movement.IndexOf("public object GcDiagnostics", StringComparison.Ordinal);
        Assert.Contains("BodyMerge.LiveMeshes(out long vertices)", movement.Substring(getter, 600));
    }
}
