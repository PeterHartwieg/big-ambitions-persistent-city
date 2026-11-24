using System;
using System.Collections.Generic;
using MTAssets.SkinnedMeshCombiner;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal static class BlendshapeBake
    {
        private static readonly BlendshapeBakePlan plan = new BlendshapeBakePlan();
        private static readonly Dictionary<Mesh, string[]> names = new Dictionary<Mesh, string[]>();
        private static readonly LengthScratch<Vector3> scratch = BlendshapeBakePlan.NewScratch<Vector3>();
        private static readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
        private static readonly List<Vector4> tangents = new List<Vector4>();
        public static long Bakes { get; private set; }
        public static long ScratchBytes => scratch.Bytes;
        public static int ScratchSizes => scratch.Count;
        public static long ScratchEvictions => scratch.Evictions;
        public static long Steps { get; private set; }

        internal static bool Adopt(SkinnedMeshCombiner? combiner)
        {
            if (combiner == null) return false;
            combiner.launchConsoleLogs = false;
            if (combiner.blendShapesSupport != SkinnedMeshCombiner.BlendShapesSupport.FullSupport) return false;
            combiner.blendShapesSupport = SkinnedMeshCombiner.BlendShapesSupport.Enabled;
            return true;
        }

        internal static void Apply(SkinnedMeshCombiner combiner, SkinnedMeshRenderer merged)
        {
            if (combiner == null || merged == null) throw new InvalidOperationException("Merged body unavailable");
            var manager = merged.GetComponent<CombinedMeshesManager>();
            Mesh target = merged.sharedMesh;
            if (manager == null || manager.sourceMeshes == null || target == null) throw new InvalidOperationException("Merged body sources unavailable");
            if (target.blendShapeCount != 0) throw new InvalidOperationException("Merged body already has blendshapes");
            var sources = manager.sourceMeshes;
            plan.Clear();
            for (int s = 0; s < sources.Length; s++)
            {
                var renderer = sources[s];
                Mesh mesh = renderer != null ? renderer.sharedMesh : null!;
                if (mesh == null) throw new InvalidOperationException("Merged body source missing");
                plan.AddSource(mesh.vertexCount);
                string[] shapes = Names(mesh);
                for (int j = 0; j < shapes.Length; j++) plan.AddShape(shapes[j], renderer!.GetBlendShapeWeight(j));
            }
            int count = target.vertexCount;
            if (!plan.Build(count, combiner.blendShapesMultiplier)) throw new InvalidOperationException("Merged body vertex layout differs");
            Bakes++;
            var steps = plan.Steps;
            if (steps.Count == 0) return;
            target.GetVertices(vertices);
            target.GetNormals(normals);
            target.GetTangents(tangents);
            if (vertices.Count != count) throw new InvalidOperationException("Merged body vertices unavailable");
            bool withNormals = normals.Count == count, withTangents = tangents.Count == count;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                Mesh mesh = sources[step.Source].sharedMesh;
                var buffer = scratch.Take(step.VertexCount);
                mesh.GetBlendShapeFrameVertices(step.Shape, mesh.GetBlendShapeFrameCount(step.Shape) - 1, buffer[0], buffer[1], buffer[2]);
                Vector3[] dv = buffer[0], dn = buffer[1], dt = buffer[2];
                float scale = step.Scale;
                for (int k = 0, at = step.Offset; k < step.VertexCount; k++, at++)
                {
                    vertices[at] += dv[k] * scale;
                    if (withNormals) normals[at] += dn[k] * scale;
                    if (withTangents)
                    {
                        Vector4 t = tangents[at];
                        tangents[at] = new Vector4(t.x + dt[k].x * scale, t.y + dt[k].y * scale, t.z + dt[k].z * scale, t.w);
                    }
                }
                Steps++;
            }
            target.SetVertices(vertices);
            ModUnityWork.MeshRequest();
            if (withNormals) target.SetNormals(normals);
            if (withTangents) target.SetTangents(tangents);
            vertices.Clear(); normals.Clear(); tangents.Clear();
        }

        private static string[] Names(Mesh mesh)
        {
            if (names.TryGetValue(mesh, out var known) && known.Length == mesh.blendShapeCount) return known;
            var found = new string[mesh.blendShapeCount];
            for (int j = 0; j < found.Length; j++) found[j] = mesh.GetBlendShapeName(j);
            names[mesh] = found;
            return found;
        }

        internal static void Clear() { names.Clear(); scratch.Clear(); plan.Clear(); }
    }
}
