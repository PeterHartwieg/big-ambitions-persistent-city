using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public readonly struct BodyMergeEntry
    {
        public BodyMergeEntry(int renderer, int submesh) { Renderer = renderer; Submesh = submesh; }
        public int Renderer { get; }
        public int Submesh { get; }
    }

    public sealed class BodyMergePlan
    {
        private readonly List<int> rendererBones = new List<int>();
        private readonly List<int> rendererBoneStart = new List<int>();
        private readonly List<int> submeshMaterial = new List<int>();
        private readonly List<BodyMergeEntry> submeshes = new List<BodyMergeEntry>();
        private readonly List<int> materialOrder = new List<int>();
        private readonly List<BodyMergeEntry> entries = new List<BodyMergeEntry>();
        private readonly List<int> groupStart = new List<int>();
        private readonly List<int> groupLength = new List<int>();
        private readonly List<int> groupBones = new List<int>();

        public int Renderers => rendererBones.Count;
        public int Groups => materialOrder.Count;
        public int Entries => entries.Count;
        public int TotalBones { get; private set; }
        public int SourceBones { get; private set; }
        public bool Built { get; private set; }

        public void Clear()
        {
            rendererBones.Clear(); rendererBoneStart.Clear(); submeshMaterial.Clear(); submeshes.Clear();
            materialOrder.Clear(); entries.Clear(); groupStart.Clear(); groupLength.Clear(); groupBones.Clear();
            TotalBones = SourceBones = 0; Built = false;
        }

        public int AddRenderer(int bones)
        {
            if (bones < 0) throw new ArgumentOutOfRangeException(nameof(bones));
            rendererBoneStart.Add(SourceBones);
            rendererBones.Add(bones);
            SourceBones += bones;
            Built = false;
            return rendererBones.Count - 1;
        }

        public void AddSubmesh(int renderer, int submesh, int material)
        {
            if (renderer < 0 || renderer >= rendererBones.Count) throw new ArgumentOutOfRangeException(nameof(renderer));
            if (submesh < 0) throw new ArgumentOutOfRangeException(nameof(submesh));
            if (material < 0) throw new ArgumentOutOfRangeException(nameof(material));
            submeshes.Add(new BodyMergeEntry(renderer, submesh));
            submeshMaterial.Add(material);
            Built = false;
        }

        public void Build()
        {
            materialOrder.Clear(); entries.Clear(); groupStart.Clear(); groupLength.Clear(); groupBones.Clear();
            TotalBones = 0;
            for (int i = 0; i < submeshMaterial.Count; i++)
                if (!materialOrder.Contains(submeshMaterial[i])) materialOrder.Add(submeshMaterial[i]);
            for (int g = 0; g < materialOrder.Count; g++)
            {
                int material = materialOrder[g], bones = 0;
                groupStart.Add(entries.Count);
                for (int i = 0; i < submeshes.Count; i++)
                {
                    if (submeshMaterial[i] != material) continue;
                    entries.Add(submeshes[i]);
                    bones += rendererBones[submeshes[i].Renderer];
                }
                groupLength.Add(entries.Count - groupStart[g]);
                groupBones.Add(bones);
                TotalBones += bones;
            }
            Built = true;
        }

        public int Material(int group) => materialOrder[group];
        public int GroupStart(int group) => groupStart[group];
        public int GroupLength(int group) => groupLength[group];
        public int GroupBones(int group) => groupBones[group];
        public BodyMergeEntry Entry(int index) => entries[index];
        public int RendererBones(int renderer) => rendererBones[renderer];
        public int RendererBoneStart(int renderer) => rendererBoneStart[renderer];
    }
}
