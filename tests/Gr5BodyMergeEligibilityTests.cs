using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5BodyMergeEligibilityTests
{
    private sealed class Part
    {
        public string Name = "";
        public bool Manager;
        public bool Renderer = true;
        public bool Enabled = true;
        public bool Mesh = true;
        public int Blendshapes;
        public int Submeshes = 1;
        public int[] Materials = { 0 };
        public bool NullMaterial;
        public int Bones = 64;
        public int Bindposes = 64;
        public int Root;
    }

    private sealed class Combiner
    {
        public bool Playing = true;
        public bool Merged;
        public bool Legacy;
        public BodyMergeBlendshapes Blendshapes = BodyMergeBlendshapes.FullSupport;
        public bool OneMeshPerMaterial = true;
        public List<string>? Ignore = new List<string> { "ShadowCaster" };
        public bool Logs;
        public bool Parameters = true;
        public bool Animator = true;
        public bool EqualRoots;
        public bool Compatibility = true;
        public List<Part> Parts = new List<Part>();
    }

    private sealed record Result(List<string> Originals, List<(string Part, int Submesh)> Combines, List<(string Part, int Bone)> Bones,
        List<(string Part, int Bone)> Poses, List<int> Materials, int Root);

    private static void Adopt(Combiner combiner)
    {
        combiner.Logs = false;
        if (combiner.Blendshapes == BodyMergeBlendshapes.FullSupport) combiner.Blendshapes = BodyMergeBlendshapes.Enabled;
    }

    private static Combiner Pedestrian(Random dice, bool female)
    {
        var combiner = new Combiner();
        combiner.Parts.Add(new Part { Name = "ShadowCaster", Materials = new[] { 9 } });
        string[] slots = female
            ? new[] { "Hair", "HairAccessory", "Head", "HeadAccessory", "Torso", "TorsoAccessory", "Legs", "LegsAccessory", "Feet", "FeetAccessory" }
            : new[] { "Hair", "Beard", "HairAccessory", "Head", "HeadAccessory", "Torso", "TorsoAccessory", "Legs", "LegsAccessory", "Feet", "FeetAccessory" };
        foreach (var slot in slots)
        {
            if (slot != "Head" && slot != "Torso" && slot != "Legs" && slot != "Feet" && dice.Next(3) == 0) continue;
            int submeshes = slot == "Head" ? 2 : 1 + (dice.Next(5) == 0 ? 1 : 0);
            combiner.Parts.Add(new Part
            {
                Name = slot,
                Submeshes = submeshes,
                Materials = Enumerable.Range(0, submeshes).Select(j => slot == "Head" && j == 0 ? 0 : dice.Next(1, 6)).ToArray(),
                Blendshapes = slot == "Head" ? 12 : dice.Next(3) == 0 ? 4 : 0,
                Bones = dice.Next(4) == 0 ? dice.Next(20, 90) : 64,
            });
        }
        foreach (var part in combiner.Parts) part.Bindposes = part.Bones;
        return combiner;
    }

    private static Result? Native(Combiner c)
    {
        if (c.Merged) return null;
        var items = c.Parts.Where(p => !p.Manager).Where(p => c.Ignore!.All(i => i != p.Name)).ToList();
        var valid = new List<Part>();
        foreach (var p in items)
        {
            if (!p.Renderer || !p.Enabled || !p.Mesh) continue;
            if (p.Blendshapes > 0 && c.Blendshapes == BodyMergeBlendshapes.Disabled) continue;
            if (p.Materials.Length == 0 || p.Materials.Length != p.Submeshes || p.NullMaterial) continue;
            valid.Add(p);
        }
        if (valid.Count < 1) return null;
        if (valid.Any(p => p.Root != valid[0].Root) && c.EqualRoots) return null;
        var groups = new Dictionary<int, List<(Part Part, int Submesh)>>();
        foreach (var p in valid)
            for (int j = 0; j < p.Submeshes; j++)
            {
                if (groups.ContainsKey(p.Materials[j])) groups[p.Materials[j]].Add((p, j));
                if (!groups.ContainsKey(p.Materials[j])) groups.Add(p.Materials[j], new List<(Part, int)> { (p, j) });
            }
        var result = new Result(items.Select(p => p.Name).ToList(), new(), new(), new(), new(), 0);
        foreach (var group in groups)
        {
            foreach (var (part, submesh) in group.Value)
            {
                for (int k = 0; k < part.Bones; k++)
                {
                    if (c.Compatibility && k >= part.Bindposes) return null;
                    result.Bones.Add((part.Name, k));
                    result.Poses.Add((part.Name, k));
                }
                result.Combines.Add((part.Name, submesh));
            }
            result.Materials.Add(group.Key);
        }
        var counts = new Dictionary<int, int>();
        foreach (var p in valid) counts[p.Root] = counts.TryGetValue(p.Root, out var n) ? n + 1 : 1;
        int best = 0, most = 0;
        foreach (var pair in counts) if (pair.Value > most) { best = pair.Key; most = pair.Value; }
        return result with { Root = best };
    }

    private static (BodyMergeFallback Reason, Result? Result) Fast(Combiner c)
    {
        var reason = BodyMergeEligibility.Check(new BodyMergeSettings(c.Playing, c.Merged, c.Legacy, c.Blendshapes, c.OneMeshPerMaterial,
            c.Ignore != null, c.Logs, c.Parameters, c.Animator));
        if (reason != BodyMergeFallback.None) return (reason, null);
        var items = c.Parts.Where(p => BodyMergeEligibility.Item(p.Manager, c.Ignore!.Contains(p.Name))).ToList();
        var sources = items.Where(p => BodyMergeEligibility.Source(
            new BodyMergeSource(p.Renderer, p.Renderer && p.Enabled, p.Mesh, p.Mesh ? p.Blendshapes : 0, p.Renderer ? p.Materials.Length : 0, p.Mesh ? p.Submeshes : 0, p.NullMaterial),
            c.Blendshapes)).ToList();
        if (sources.Count < 1) return (BodyMergeFallback.NoSources, null);
        if (sources.Any(p => p.Root != sources[0].Root) && c.EqualRoots) return (BodyMergeFallback.DifferentRootBones, null);
        var plan = new BodyMergePlan();
        var materials = new List<int>();
        for (int r = 0; r < sources.Count; r++)
        {
            if (c.Compatibility && sources[r].Bindposes < sources[r].Bones) return (BodyMergeFallback.ShortBindposes, null);
            plan.AddRenderer(sources[r].Bones);
            for (int j = 0; j < sources[r].Submeshes; j++)
            {
                int key = materials.IndexOf(sources[r].Materials[j]);
                if (key < 0) { materials.Add(sources[r].Materials[j]); key = materials.Count - 1; }
                plan.AddSubmesh(r, j, key);
            }
        }
        plan.Build();
        if (plan.Groups == 0) return (BodyMergeFallback.NoGroups, null);
        var result = new Result(items.Select(p => p.Name).ToList(), new(), new(), new(), new(), 0);
        for (int g = 0; g < plan.Groups; g++)
        {
            for (int e = 0; e < plan.GroupLength(g); e++)
            {
                var entry = plan.Entry(plan.GroupStart(g) + e);
                var part = sources[entry.Renderer];
                for (int k = 0; k < plan.RendererBones(entry.Renderer); k++) { result.Bones.Add((part.Name, k)); result.Poses.Add((part.Name, k)); }
                result.Combines.Add((part.Name, entry.Submesh));
            }
            result.Materials.Add(materials[plan.Material(g)]);
        }
        var roots = new List<int>();
        var rootCounts = new List<int>();
        foreach (var p in sources)
        {
            int at = roots.IndexOf(p.Root);
            if (at < 0) { roots.Add(p.Root); rootCounts.Add(1); } else rootCounts[at]++;
        }
        int best = 0, most = 0;
        for (int i = 0; i < roots.Count; i++) if (rootCounts[i] > most) { best = roots[i]; most = rootCounts[i]; }
        return (BodyMergeFallback.None, result with { Root = best });
    }

    private static void AssertSame(Result native, Result fast)
    {
        Assert.Equal(native.Originals, fast.Originals);
        Assert.Equal(native.Combines, fast.Combines);
        Assert.Equal(native.Bones, fast.Bones);
        Assert.Equal(native.Poses, fast.Poses);
        Assert.Equal(native.Materials, fast.Materials);
        Assert.Equal(native.Root, fast.Root);
    }

    [Fact]
    public void TheShippedPedestrianPrefabIsEligibleForTheFastMergeAfterAdoption()
    {
        var dice = new Random(0x16D);
        foreach (bool female in new[] { false, true })
        {
            var combiner = Pedestrian(dice, female);
            Assert.Equal(BodyMergeFallback.FullBlendshapes, Fast(combiner).Reason);
            Adopt(combiner);
            var (reason, fast) = Fast(combiner);
            Assert.Equal(BodyMergeFallback.None, reason);
            Assert.DoesNotContain("ShadowCaster", fast!.Originals);
            AssertSame(Native(combiner)!, fast);
        }
    }

    [Fact]
    public void FastMergesMatchTheNativeCombinerOnRepresentativePedestrianBodies()
    {
        var dice = new Random(0x16E);
        int merged = 0;
        for (int round = 0; round < 400; round++)
        {
            var combiner = Pedestrian(dice, round % 2 == 1);
            Adopt(combiner);
            foreach (var part in combiner.Parts)
            {
                if (dice.Next(12) == 0) part.Enabled = false;
                if (dice.Next(20) == 0) part.Manager = true;
                if (dice.Next(25) == 0) part.NullMaterial = true;
                if (dice.Next(25) == 0) part.Materials = part.Materials.Take(Math.Max(0, part.Materials.Length - 1)).ToArray();
                if (dice.Next(15) == 0) part.Root = 1;
            }
            if (round % 7 == 0) combiner.Blendshapes = BodyMergeBlendshapes.Disabled;
            if (round % 11 == 0) combiner.EqualRoots = true;
            if (round % 13 == 0) combiner.Ignore!.Add("Hair");
            var native = Native(combiner);
            var (reason, fast) = Fast(combiner);
            if (native == null) { Assert.NotEqual(BodyMergeFallback.None, reason); continue; }
            Assert.Equal(BodyMergeFallback.None, reason);
            AssertSame(native, fast!);
            merged++;
        }
        Assert.True(merged > 300, merged.ToString());
    }

    [Fact]
    public void EachUnsupportedCombinerSettingNamesItsOwnFallbackReason()
    {
        var cases = new (Action<Combiner> Change, BodyMergeFallback Reason)[]
        {
            (c => c.Playing = false, BodyMergeFallback.NotPlaying),
            (c => c.Merged = true, BodyMergeFallback.AlreadyMerged),
            (c => c.Legacy = true, BodyMergeFallback.LegacyAnimation),
            (c => c.Blendshapes = BodyMergeBlendshapes.FullSupport, BodyMergeFallback.FullBlendshapes),
            (c => c.OneMeshPerMaterial = false, BodyMergeFallback.OtherMethod),
            (c => c.Ignore = null, BodyMergeFallback.IgnoreListMissing),
            (c => c.Logs = true, BodyMergeFallback.ConsoleLogs),
            (c => c.Parameters = false, BodyMergeFallback.ParamsMissing),
            (c => c.Animator = false, BodyMergeFallback.NoAnimator),
            (c => { foreach (var p in c.Parts) p.Enabled = false; }, BodyMergeFallback.NoSources),
            (c => { c.EqualRoots = true; c.Parts.Last().Root = 3; }, BodyMergeFallback.DifferentRootBones),
            (c => c.Parts.Last().Bindposes = 1, BodyMergeFallback.ShortBindposes),
        };
        foreach (var (change, reason) in cases)
        {
            var combiner = Pedestrian(new Random(7), false);
            Adopt(combiner);
            change(combiner);
            Assert.Equal(reason, Fast(combiner).Reason);
        }
    }

    [Fact]
    public void TheFallbackCounterReportsEveryCauseByName()
    {
        var fallbacks = new BodyMergeFallbacks();
        Assert.Equal("none", fallbacks.Describe());
        fallbacks.Count(BodyMergeFallback.NoSources);
        fallbacks.Count(BodyMergeFallback.NoSources);
        fallbacks.Count(BodyMergeFallback.CollectError);
        Assert.Equal(3, fallbacks.Total);
        Assert.Equal(2, fallbacks[BodyMergeFallback.NoSources]);
        Assert.Equal("no sources 2, collect error 1", fallbacks.Describe());
        Assert.Throws<ArgumentOutOfRangeException>(() => fallbacks.Count(BodyMergeFallback.None));
        foreach (BodyMergeFallback reason in Enum.GetValues(typeof(BodyMergeFallback)))
            if (reason != BodyMergeFallback.None) fallbacks.Count(reason);
        Assert.DoesNotContain("  ", fallbacks.Describe());
        Assert.Equal(Enum.GetValues(typeof(BodyMergeFallback)).Length - 1, fallbacks.Describe().Split(", ").Length);
    }

    [Fact]
    public void CountingFallbacksAllocatesNothing()
    {
        var fallbacks = new BodyMergeFallbacks();
        fallbacks.Count(BodyMergeFallback.NoSources);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            fallbacks.Count(BodyMergeFallback.MergeError);
            BodyMergeEligibility.Check(new BodyMergeSettings(true, false, false, BodyMergeBlendshapes.Enabled, true, true, false, true, true));
            BodyMergeEligibility.Source(new BodyMergeSource(true, true, true, 0, 1, 1, false), BodyMergeBlendshapes.Enabled);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
