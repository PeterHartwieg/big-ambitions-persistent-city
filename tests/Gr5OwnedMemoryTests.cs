using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using PersistentCity.Core;

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
        Assert.Contains("var tracked = ledger.Forget(combiner);", releaseBody);
        Assert.Contains("combiner.resultMergeGameObject", releaseBody);
        Assert.Contains("if (tracked != mesh) DestroyMesh(tracked);", releaseBody);
        Assert.Contains("Object.Destroy(mesh);", releaseBody);
        Assert.DoesNotContain("ledger.Prune();", releaseBody);
        Assert.DoesNotContain("ledger.ReleaseOrphans();", releaseBody);
        Assert.Contains("if (combiner == null) { ledger.ReleaseOrphan(combiner); return; }", releaseBody);
        Assert.Contains("ledger.CountReleased(mesh);\n            Object.Destroy(mesh);", releaseBody.Replace("\r", ""));
        int keyed = merge.IndexOf("ledger.Merged(combiner, combined);", StringComparison.Ordinal);
        Assert.True(keyed > 0, "a redress replaces the combiner's previous mesh instead of adding another");
        Assert.True(keyed < merge.IndexOf("Fast++;", keyed, StringComparison.Ordinal), "only a finished merge is tracked, so Discard never leaves an entry");
        Assert.DoesNotContain("HashSet<Mesh>", merge);
        Assert.Contains("ledger.PruneOwned();\n            ledger.ReleaseOrphans();\n            if (hooked", merge.Replace("\r", ""));
    }

    private sealed class NativeHandle
    {
        public bool Alive = true;
    }

    [Fact]
    public void ADeadCombinerWithALiveMeshReleasesTheMeshOnceWhileLiveCombinerCountsStayIntact()
    {
        var destroyed = new List<NativeHandle>();
        var ledger = new MergedBodyLedger<NativeHandle, NativeHandle>(body => body.Alive, mesh => mesh.Alive, _ => 37, mesh =>
        {
            destroyed.Add(mesh);
            mesh.Alive = false;
        });
        var survivor = new NativeHandle();
        var survivorMesh = new NativeHandle();
        ledger.Own(survivor);
        ledger.Merged(survivor, survivorMesh);
        for (int i = 0; i < 5; i++)
        {
            var body = new NativeHandle();
            var mesh = new NativeHandle();
            ledger.Own(body);
            ledger.Merged(body, mesh);
            body.Alive = false;
            Assert.Equal(2, ledger.LiveMeshes(out long retained));
            Assert.Equal(74, retained);
            Assert.Equal(i, ledger.ReleasedMeshes);
            Assert.Equal(i, destroyed.Count);
            ledger.ReleaseOrphans();
            Assert.Equal(1, ledger.LiveMeshes(out long vertices));
            Assert.Equal(37, vertices);
            Assert.Equal(1, ledger.Owned);
            Assert.Equal(0, ledger.Unmerged);
            Assert.Same(mesh, destroyed[i]);
            Assert.Equal(i + 1, ledger.ReleasedMeshes);
            Assert.Equal((i + 1) * 37, ledger.ReleasedVertices);
        }
        Assert.True(survivorMesh.Alive);
        Assert.Same(survivorMesh, ledger.Forget(survivor));
        Assert.Null(ledger.Forget(survivor));
    }

    [Fact]
    public void ADeadCombinerWhoseMeshALiveRendererStillShowsKeepsThatMesh()
    {
        var destroyed = new List<NativeHandle>();
        var shown = new HashSet<NativeHandle>();
        var ledger = new MergedBodyLedger<NativeHandle, NativeHandle>(body => body.Alive, mesh => mesh.Alive, _ => 37,
            mesh => { destroyed.Add(mesh); mesh.Alive = false; }, (_, mesh) => shown.Contains(mesh));
        var kept = new NativeHandle();
        var keptMesh = new NativeHandle();
        var orphan = new NativeHandle();
        var orphanMesh = new NativeHandle();
        ledger.Own(kept); ledger.Merged(kept, keptMesh);
        ledger.Own(orphan); ledger.Merged(orphan, orphanMesh);
        shown.Add(keptMesh);
        kept.Alive = false; orphan.Alive = false;
        Assert.Equal(2, ledger.LiveMeshes(out _));
        Assert.Empty(destroyed);
        Assert.Equal(0, ledger.ReleasedMeshes);
        ledger.ReleaseOrphans();
        Assert.Equal(new[] { orphanMesh }, destroyed);
        Assert.True(keptMesh.Alive);
        Assert.Equal(1, ledger.ReleasedMeshes);
        ledger.PruneOwned();
        Assert.Equal(0, ledger.Owned);
        Assert.Equal(1, ledger.LiveMeshes(out long shownVertices));
        Assert.Equal(37, shownVertices);
        ledger.ReleaseOrphans();
        Assert.Equal(1, ledger.ReleasedMeshes);
        shown.Remove(keptMesh);
        Assert.Equal(1, ledger.LiveMeshes(out _));
        Assert.Equal(1, ledger.ReleasedMeshes);
        ledger.ReleaseOrphans();
        Assert.Equal(new[] { orphanMesh, keptMesh }, destroyed);
        Assert.Equal(2, ledger.ReleasedMeshes);
        Assert.Equal(74, ledger.ReleasedVertices);
        Assert.Equal(0, ledger.LiveMeshes(out _));
        ledger.ReleaseOrphans();
        Assert.Equal(2, ledger.ReleasedMeshes);
    }

    [Fact]
    public void ReleasingOneDeadCombinerTouchesOnlyItsOwnEntryAndKeepsAMeshALiveRendererShows()
    {
        var destroyed = new List<NativeHandle>();
        var shown = new HashSet<NativeHandle>();
        var ledger = new MergedBodyLedger<NativeHandle, NativeHandle>(body => body.Alive, mesh => mesh.Alive, _ => 37,
            mesh => { destroyed.Add(mesh); mesh.Alive = false; }, (_, mesh) => shown.Contains(mesh));
        var released = new NativeHandle(); var releasedMesh = new NativeHandle();
        var other = new NativeHandle(); var otherMesh = new NativeHandle();
        var kept = new NativeHandle(); var keptMesh = new NativeHandle();
        var live = new NativeHandle(); var liveMesh = new NativeHandle();
        foreach (var (body, mesh) in new[] { (released, releasedMesh), (other, otherMesh), (kept, keptMesh), (live, liveMesh) }) { ledger.Own(body); ledger.Merged(body, mesh); }
        released.Alive = false; other.Alive = false; kept.Alive = false;
        shown.Add(keptMesh);
        ledger.ReleaseOrphan(released);
        Assert.Equal(new[] { releasedMesh }, destroyed);
        Assert.True(otherMesh.Alive);
        ledger.ReleaseOrphan(kept);
        Assert.Equal(new[] { releasedMesh }, destroyed);
        Assert.True(keptMesh.Alive);
        ledger.ReleaseOrphan(live);
        ledger.ReleaseOrphan(released);
        Assert.Equal(1, ledger.ReleasedMeshes);
        Assert.Equal(3, ledger.LiveMeshes(out _));
        shown.Remove(keptMesh);
        ledger.ReleaseOrphans();
        Assert.Equal(new[] { releasedMesh, otherMesh, keptMesh }, destroyed);
        Assert.True(liveMesh.Alive);
        Assert.Equal(1, ledger.LiveMeshes(out _));
    }

    [Fact]
    public void AKeptOrphanMeshIsReleasedByLaterBodyReleasesOnceItsRendererStopsShowingIt()
    {
        var destroyed = new List<NativeHandle>();
        var shown = new HashSet<NativeHandle>();
        var ledger = new MergedBodyLedger<NativeHandle, NativeHandle>(body => body.Alive, mesh => mesh.Alive, _ => 37,
            mesh => { destroyed.Add(mesh); mesh.Alive = false; }, (_, mesh) => shown.Contains(mesh));
        var kept = new NativeHandle(); var keptMesh = new NativeHandle();
        var second = new NativeHandle(); var secondMesh = new NativeHandle();
        var live = new NativeHandle(); var liveMesh = new NativeHandle();
        foreach (var (body, mesh) in new[] { (kept, keptMesh), (second, secondMesh), (live, liveMesh) }) { ledger.Own(body); ledger.Merged(body, mesh); }
        kept.Alive = false; second.Alive = false;
        shown.Add(keptMesh); shown.Add(secondMesh);
        ledger.ReleaseOrphan(kept);
        ledger.ReleaseOrphan(second);
        ledger.ReleaseOrphan(kept);
        Assert.Empty(destroyed);
        Assert.Equal(2, ledger.Deferred);
        ledger.RetryDeferred(2);
        Assert.Empty(destroyed);
        Assert.Equal(2, ledger.Deferred);
        shown.Remove(keptMesh);
        ledger.RetryDeferred(1);
        Assert.Equal(new[] { keptMesh }, destroyed);
        Assert.Equal(1, ledger.Deferred);
        Assert.True(secondMesh.Alive);
        Assert.True(liveMesh.Alive);
        Assert.Equal(2, ledger.LiveMeshes(out _));
        ledger.RetryDeferred(2);
        Assert.Equal(1, ledger.Deferred);
        shown.Remove(secondMesh);
        ledger.ReleaseOrphans();
        Assert.Equal(new[] { keptMesh, secondMesh }, destroyed);
        Assert.Equal(0, ledger.Deferred);
        Assert.Equal(2, ledger.ReleasedMeshes);
        Assert.True(liveMesh.Alive);
        string merge = Source("Runtime", "BodyMerge.cs").Replace("\r", "");
        Assert.Contains("if (ReferenceEquals(combiner, null)) return;\n            ledger.RetryDeferred(DeferredRetries);\n            if (combiner == null)", merge);
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
            "bakeScratchBytes = BlendshapeBake.ScratchBytes", "orphanMergedMeshes = orphans", "orphanMergedVertices = orphanVertices",
            "deferredOrphanMeshes = BodyMerge.DeferredOrphans" })
            Assert.Contains(field, movement);
        int getter = movement.IndexOf("public object GcDiagnostics", StringComparison.Ordinal);
        Assert.Contains("BodyMerge.LiveMeshes(out long vertices)", movement.Substring(getter, 600));
        Assert.Contains("BodyMerge.OrphanMeshes(out long orphanVertices)", movement.Substring(getter, 700));
    }

    [Fact]
    public void OrphanMeshCountsSeparateDeadCombinersFromLiveBodiesUntilTheirMeshesAreReleased()
    {
        var destroyed = new List<NativeHandle>();
        var shown = new HashSet<NativeHandle>();
        var ledger = new MergedBodyLedger<NativeHandle, NativeHandle>(body => body.Alive, mesh => mesh.Alive, _ => 37,
            mesh => { destroyed.Add(mesh); mesh.Alive = false; }, (_, mesh) => shown.Contains(mesh));
        var bodies = Enumerable.Range(0, 4).Select(_ => (Body: new NativeHandle(), Mesh: new NativeHandle())).ToArray();
        foreach (var (body, mesh) in bodies) { ledger.Own(body); ledger.Merged(body, mesh); }
        Assert.Equal(0, ledger.OrphanMeshes(out long none));
        Assert.Equal(0, none);
        bodies[0].Body.Alive = false; bodies[1].Body.Alive = false;
        shown.Add(bodies[1].Mesh);
        Assert.Equal(4, ledger.LiveMeshes(out long all));
        Assert.Equal(4 * 37, all);
        Assert.Equal(2, ledger.OrphanMeshes(out long orphanVertices));
        Assert.Equal(2 * 37, orphanVertices);
        ledger.ReleaseOrphan(bodies[0].Body);
        ledger.ReleaseOrphan(bodies[1].Body);
        Assert.Equal(new[] { bodies[0].Mesh }, destroyed);
        Assert.Equal(1, ledger.OrphanMeshes(out _));
        Assert.Equal(1, ledger.Deferred);
        Assert.Equal(3, ledger.LiveMeshes(out _));
        shown.Clear();
        ledger.RetryDeferred(2);
        Assert.Equal(0, ledger.OrphanMeshes(out _));
        Assert.Equal(0, ledger.Deferred);
        Assert.Equal(2, ledger.LiveMeshes(out _));
        Assert.Equal(2, ledger.ReleasedMeshes);
        Assert.Equal(2, ledger.Owned);
    }
}
