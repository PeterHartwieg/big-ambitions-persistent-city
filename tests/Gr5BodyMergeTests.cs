using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5BodyMergeTests
{
    private const long Rig15dDayAppearanceBytes = 33456448;
    private const int Rig15dDayBuilds = 208;
    private const int Header = 32, Pointer = 8, Pose = 64, Combine = 80;
    private readonly ITestOutputHelper output;
    public Gr5BodyMergeTests(ITestOutputHelper output) { this.output = output; }

    private sealed class Body
    {
        public int[] Bones = Array.Empty<int>();
        public int[][] Materials = Array.Empty<int[]>();
    }

    private static Body RandomBody(Random dice)
    {
        int renderers = 1 + dice.Next(14);
        var body = new Body { Bones = new int[renderers], Materials = new int[renderers][] };
        for (int r = 0; r < renderers; r++)
        {
            body.Bones[r] = dice.Next(4) == 0 ? dice.Next(1, 120) : 64;
            body.Materials[r] = Enumerable.Range(0, 1 + dice.Next(3)).Select(_ => dice.Next(6)).ToArray();
        }
        return body;
    }

    private static List<(int Material, List<(int Renderer, int Submesh)> Entries)> NativeGrouping(Body body)
    {
        var groups = new Dictionary<int, List<(int, int)>>();
        for (int r = 0; r < body.Bones.Length; r++)
            for (int j = 0; j < body.Materials[r].Length; j++)
            {
                int material = body.Materials[r][j];
                if (groups.ContainsKey(material)) groups[material].Add((r, j));
                if (!groups.ContainsKey(material)) groups.Add(material, new List<(int, int)> { (r, j) });
            }
        return groups.Select(g => (g.Key, g.Value)).ToList();
    }

    private static void Fill(BodyMergePlan plan, Body body)
    {
        plan.Clear();
        for (int r = 0; r < body.Bones.Length; r++)
        {
            int at = plan.AddRenderer(body.Bones[r]);
            for (int j = 0; j < body.Materials[r].Length; j++) plan.AddSubmesh(at, j, body.Materials[r][j]);
        }
        plan.Build();
    }

    [Fact]
    public void ThePlanGroupsSubmeshesAndBonesInTheNativeCombinersOrder()
    {
        var dice = new Random(0x15D);
        var plan = new BodyMergePlan();
        for (int round = 0; round < 500; round++)
        {
            var body = RandomBody(dice);
            Fill(plan, body);
            var native = NativeGrouping(body);
            Assert.Equal(native.Count, plan.Groups);
            var nativeBones = new List<(int Renderer, int Bone)>();
            var planBones = new List<(int Renderer, int Bone)>();
            for (int g = 0; g < native.Count; g++)
            {
                Assert.Equal(native[g].Material, plan.Material(g));
                Assert.Equal(native[g].Entries.Count, plan.GroupLength(g));
                int groupBones = 0;
                for (int e = 0; e < native[g].Entries.Count; e++)
                {
                    var entry = plan.Entry(plan.GroupStart(g) + e);
                    Assert.Equal(native[g].Entries[e], (entry.Renderer, entry.Submesh));
                    for (int k = 0; k < body.Bones[native[g].Entries[e].Renderer]; k++) nativeBones.Add((native[g].Entries[e].Renderer, k));
                    for (int k = 0; k < plan.RendererBones(entry.Renderer); k++) planBones.Add((entry.Renderer, k));
                    groupBones += body.Bones[entry.Renderer];
                }
                Assert.Equal(groupBones, plan.GroupBones(g));
            }
            Assert.Equal(nativeBones, planBones);
            Assert.Equal(nativeBones.Count, plan.TotalBones);
            Assert.Equal(body.Bones.Sum(), plan.SourceBones);
            for (int r = 0; r < body.Bones.Length; r++) Assert.Equal(body.Bones.Take(r).Sum(), plan.RendererBoneStart(r));
        }
    }

    [Fact]
    public void PlanningABodyMergeAllocatesNothingAfterWarmUp()
    {
        var dice = new Random(0x15E);
        var bodies = Enumerable.Range(0, 64).Select(_ => RandomBody(dice)).ToArray();
        var plan = new BodyMergePlan();
        foreach (var body in bodies) Fill(plan, body);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int round = 0; round < 10; round++) foreach (var body in bodies) Fill(plan, body);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static long Bytes(long length, int element) => Header + length * element;

    private static long NativeMergeBytes(int renderers, int submeshesPerRenderer, int materials, int bones)
    {
        int submeshes = renderers * submeshesPerRenderer, perMaterial = submeshes / materials;
        long total = (long)submeshes * bones;
        long items = Bytes(renderers, Pointer) * 4;
        long validate = renderers * (4 + submeshesPerRenderer) * Bytes(submeshesPerRenderer, Pointer);
        long grouping = submeshes * (3 * Bytes(submeshesPerRenderer, Pointer) + 40) + materials * Bytes(4, Pointer);
        long sources = submeshes * (Bytes(bones, Pointer) + Bytes(bones, Pose));
        long groups = materials * (Bytes(bones, Pointer) + Bytes(perMaterial, Combine) + 2 * Bytes((long)perMaterial * bones, Pose)
            + 2 * Bytes((long)perMaterial * bones, Pointer) + 56 + 24) + 200;
        long final = Bytes(materials, Combine) + Bytes(total, Pointer) + Bytes(total, Pose) + Bytes(materials, Pointer) + 200 + 400;
        return items + validate + grouping + sources + groups + final;
    }

    private static long OwnMergeBytes(int renderers, int bones, int materials) =>
        2 * Bytes(renderers, Pointer) + renderers * Bytes(bones, Pointer) + (materials + 1) * 24 + 400;

    [Fact]
    public void TheOwnMergeModelCutsTheCombinersPerBuildCopiesByMoreThanNinetyPercent()
    {
        double perBuild = (double)Rig15dDayAppearanceBytes / Rig15dDayBuilds;
        int calibrated = 1;
        for (int bones = 1; bones < 400; bones++)
            if (Math.Abs(NativeMergeBytes(10, 1, 4, bones) - perBuild) < Math.Abs(NativeMergeBytes(10, 1, 4, calibrated) - perBuild)) calibrated = bones;
        foreach (var (renderers, submeshes, materials, bones) in new[] { (10, 1, 4, calibrated), (8, 2, 4, calibrated / 2), (12, 1, 6, calibrated), (10, 1, 4, 64) })
        {
            long native = NativeMergeBytes(renderers, submeshes, materials, bones), own = OwnMergeBytes(renderers, bones, materials);
            output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "renderers={0} submeshes/renderer={1} materials={2} bones={3}: native {4:0.0} KB/merge, own {5:0.0} KB/merge, day window {6:0.0} -> {7:0.0} MB at {8} builds",
                renderers, submeshes, materials, bones, native / 1024.0, own / 1024.0, native * Rig15dDayBuilds / 1e6, own * Rig15dDayBuilds / 1e6, Rig15dDayBuilds));
            Assert.True(own * 10 < native, own + " vs " + native);
        }
        output.WriteLine("rig 15d day measured " + (perBuild / 1024).ToString("0.0", CultureInfo.InvariantCulture) + " KB per build; calibrated bones per renderer " + calibrated);
    }
}
