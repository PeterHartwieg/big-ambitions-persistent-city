using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MTAssets.SkinnedMeshCombiner;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PersistentCity.Runtime
{
    internal static class BodyMerge
    {
        private const string Owner = "hart.persistentcity.bodymerge.v1";
        private const int MaximumCachedLengths = 64;
        private static readonly Harmony harmony = new Harmony(Owner);
        private static readonly MethodInfo? Target = AccessTools.Method(typeof(SkinnedMeshCombiner), "DoCombineMeshs_OneMeshPerMaterial");
        private static readonly MethodInfo? ValidateMethod = AccessTools.Method(typeof(SkinnedMeshCombiner), "ValidateAllVariables");
        private static readonly MethodInfo? StopMethod = AccessTools.Method(typeof(SkinnedMeshCombiner), "StopMergeByErrorWhileMerging", new[] { typeof(Exception) });
        private static readonly FieldInfo? PositionField = AccessTools.Field(typeof(SkinnedMeshCombiner), "thisOriginalPosition");
        private static readonly FieldInfo? RotationField = AccessTools.Field(typeof(SkinnedMeshCombiner), "thisOriginalRotation");
        private static readonly FieldInfo? ScaleField = AccessTools.Field(typeof(SkinnedMeshCombiner), "thisOriginalScale");
        private static Action<SkinnedMeshCombiner>? validate;
        private static Action<SkinnedMeshCombiner, Exception>? stop;
        private static AccessTools.FieldRef<SkinnedMeshCombiner, Vector3>? originalPosition, originalRotation, originalScale;
        private static bool hooked;
        private static readonly HashSet<SkinnedMeshCombiner> owned = new HashSet<SkinnedMeshCombiner>();
        private static readonly BodyMergePlan plan = new BodyMergePlan();
        private static readonly List<SkinnedMeshRenderer> found = new List<SkinnedMeshRenderer>();
        private static readonly List<SkinnedMeshRenderer> items = new List<SkinnedMeshRenderer>();
        private static readonly List<SkinnedMeshRenderer> sources = new List<SkinnedMeshRenderer>();
        private static readonly List<Transform[]> sourceBones = new List<Transform[]>();
        private static readonly List<Matrix4x4> sourcePoses = new List<Matrix4x4>();
        private static readonly List<Matrix4x4> poseScratch = new List<Matrix4x4>();
        private static readonly List<Material> materialScratch = new List<Material>();
        private static readonly List<Material> materials = new List<Material>();
        private static readonly List<Transform> finalBones = new List<Transform>();
        private static readonly List<Matrix4x4> finalPoses = new List<Matrix4x4>();
        private static readonly List<Mesh> groupMeshes = new List<Mesh>();
        private static readonly List<Transform> roots = new List<Transform>();
        private static readonly List<int> rootCounts = new List<int>();
        private static readonly Dictionary<int, CombineInstance[]> combines = new Dictionary<int, CombineInstance[]>();
        private static readonly Dictionary<int, Matrix4x4[]> poses = new Dictionary<int, Matrix4x4[]>();
        private static readonly Dictionary<int, Transform[]> bones = new Dictionary<int, Transform[]>();
        private static readonly Dictionary<int, Material[]> materialArrays = new Dictionary<int, Material[]>();

        public static long Fast { get; private set; }
        public static long Native { get; private set; }
        public static long Failed { get; private set; }
        public static string LastError { get; private set; } = "";
        public static bool Hooked => hooked;
        public static int Owned => owned.Count;

        public static void Install()
        {
            if (hooked || LastError.Length != 0) return;
            try
            {
                if (Target == null || ValidateMethod == null || StopMethod == null || PositionField == null || RotationField == null || ScaleField == null
                    || PositionField.FieldType != typeof(Vector3) || RotationField.FieldType != typeof(Vector3) || ScaleField.FieldType != typeof(Vector3))
                    throw new InvalidOperationException("Unexpected native body combiner contract.");
                if (Harmony.HasAnyPatches(Owner)) throw new InvalidOperationException("Another body merge hook owner is already registered.");
                validate = AccessTools.MethodDelegate<Action<SkinnedMeshCombiner>>(ValidateMethod);
                stop = AccessTools.MethodDelegate<Action<SkinnedMeshCombiner, Exception>>(StopMethod);
                originalPosition = AccessTools.FieldRefAccess<SkinnedMeshCombiner, Vector3>(PositionField);
                originalRotation = AccessTools.FieldRefAccess<SkinnedMeshCombiner, Vector3>(RotationField);
                originalScale = AccessTools.FieldRefAccess<SkinnedMeshCombiner, Vector3>(ScaleField);
                harmony.Patch(Target, prefix: new HarmonyMethod(typeof(BodyMerge), nameof(MergePrefix)));
                hooked = true;
            }
            catch (Exception error) { LastError = error.GetType().Name + ": " + error.Message; }
        }

        public static void Own(SkinnedMeshCombiner? combiner) { if (combiner != null) owned.Add(combiner); }

        public static void Forget(SkinnedMeshCombiner? combiner) { if (!ReferenceEquals(combiner, null)) owned.Remove(combiner!); }

        private static bool MergePrefix(SkinnedMeshCombiner __instance)
        {
            if (!owned.Contains(__instance)) return true;
            bool collected;
            try { collected = Collect(__instance); }
            catch (Exception) { collected = false; }
            if (!collected) { Release(); Native++; return true; }
            Merge(__instance);
            Release();
            return false;
        }

        private static bool Collect(SkinnedMeshCombiner combiner)
        {
            Release();
            if (!Application.isPlaying || combiner.resultMergeGameObject != null || combiner.legacyAnimationSupport
                || combiner.blendShapesSupport == SkinnedMeshCombiner.BlendShapesSupport.FullSupport
                || combiner.mergeMethod != SkinnedMeshCombiner.MergeMethod.OneMeshPerMaterial
                || combiner.gameObjectsToIgnore == null || combiner.gameObjectsToIgnore.Count != 0 || combiner.launchConsoleLogs
                || combiner.oneMeshPerMaterialParams == null || combiner.GetComponent<Animator>() == null) return false;
            combiner.gameObject.GetComponentsInChildren(combiner.combineInactives, found);
            foreach (var renderer in found)
                if (renderer.gameObject.GetComponent<CombinedMeshesManager>() == null) items.Add(renderer);
            foreach (var item in items)
            {
                var component = item.gameObject.GetComponent<SkinnedMeshRenderer>();
                if (component == null || !component.enabled) continue;
                var mesh = component.sharedMesh;
                if (mesh == null) continue;
                if (mesh.blendShapeCount > 0 && combiner.blendShapesSupport == SkinnedMeshCombiner.BlendShapesSupport.Disabled) continue;
                component.GetSharedMaterials(materialScratch);
                if (materialScratch.Count == 0 || materialScratch.Count != mesh.subMeshCount) continue;
                bool nulls = false;
                foreach (var material in materialScratch) if (material == null) nulls = true;
                if (nulls) continue;
                sources.Add(component);
            }
            if (sources.Count < 1) return false;
            var rootBone = sources[0].rootBone;
            bool differentRoots = false;
            foreach (var source in sources) if (rootBone != source.rootBone) { differentRoots = true; break; }
            if (differentRoots && combiner.oneMeshPerMaterialParams.mergeOnlyEqualRootBones) return false;
            for (int r = 0; r < sources.Count; r++)
            {
                var source = sources[r];
                var mesh = source.sharedMesh;
                var rendererBones = source.bones;
                if (rendererBones == null) return false;
                if (combiner.compatibilityMode)
                {
                    mesh.GetBindposes(poseScratch);
                    if (poseScratch.Count < rendererBones.Length) return false;
                    for (int k = 0; k < rendererBones.Length; k++) sourcePoses.Add(poseScratch[k]);
                }
                sourceBones.Add(rendererBones);
                plan.AddRenderer(rendererBones.Length);
                source.GetSharedMaterials(materialScratch);
                for (int j = 0; j < mesh.subMeshCount; j++) plan.AddSubmesh(r, j, MaterialKey(materialScratch[j]));
            }
            plan.Build();
            return plan.Groups > 0;
        }

        private static int MaterialKey(Material material)
        {
            for (int i = 0; i < materials.Count; i++) if (ReferenceEquals(materials[i], material)) return i;
            materials.Add(material);
            return materials.Count - 1;
        }

        private static void Merge(SkinnedMeshCombiner combiner)
        {
            var root = combiner.gameObject.transform;
            if (combiner.autoManagePosition)
            {
                originalPosition!(combiner) = root.position;
                originalRotation!(combiner) = root.eulerAngles;
                originalScale!(combiner) = root.localScale;
                root.position = Vector3.zero;
                root.eulerAngles = Vector3.zero;
                root.localScale = Vector3.one;
            }
            try
            {
                combiner.resultMergeLogs.Clear();
                validate!(combiner);
                var animator = combiner.GetComponent<Animator>();
                var cullingMode = animator.cullingMode;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                int vertices = 0;
                foreach (var source in sources) vertices += source.sharedMesh.vertexCount;
                var format = vertices <= 50000 ? IndexFormat.UInt16 : IndexFormat.UInt32;
                for (int g = 0; g < plan.Groups; g++)
                {
                    int start = plan.GroupStart(g), length = plan.GroupLength(g), poseStart = finalPoses.Count;
                    var parts = Array(combines, length);
                    for (int e = 0; e < length; e++)
                    {
                        var entry = plan.Entry(start + e);
                        var source = sources[entry.Renderer];
                        var rendererBones = sourceBones[entry.Renderer];
                        var transform = source.transform;
                        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
                        int at = plan.RendererBoneStart(entry.Renderer);
                        for (int k = 0; k < rendererBones.Length; k++)
                        {
                            finalBones.Add(rendererBones[k]);
                            finalPoses.Add(combiner.compatibilityMode ? sourcePoses[at + k] * worldToLocal : rendererBones[k].worldToLocalMatrix * worldToLocal);
                        }
                        parts[e] = new CombineInstance { mesh = source.sharedMesh, subMeshIndex = entry.Submesh, transform = transform.localToWorldMatrix };
                    }
                    var mesh = new Mesh { indexFormat = format };
                    mesh.CombineMeshes(parts, mergeSubMeshes: true, useMatrices: true);
                    System.Array.Clear(parts, 0, parts.Length);
                    groupMeshes.Add(mesh);
                    var groupPoses = Array(poses, finalPoses.Count - poseStart);
                    for (int k = 0; k < groupPoses.Length; k++) groupPoses[k] = finalPoses[poseStart + k];
                    mesh.bindposes = groupPoses;
                }
                var final = Array(combines, groupMeshes.Count);
                for (int g = 0; g < groupMeshes.Count; g++) final[g] = new CombineInstance { mesh = groupMeshes[g], subMeshIndex = 0, transform = Matrix4x4.identity };
                var combined = new Mesh { indexFormat = format, name = "Combined Meshes (One Mesh Per Material)" };
                combined.CombineMeshes(final, mergeSubMeshes: false);
                System.Array.Clear(final, 0, final.Length);
                foreach (var mesh in groupMeshes) Object.Destroy(mesh);
                groupMeshes.Clear();
                combined.RecalculateBounds();
                var result = new GameObject(combiner.nameOfThisMerge);
                combiner.resultMergeGameObject = result;
                result.transform.SetParent(root);
                var merged = result.AddComponent<SkinnedMeshRenderer>();
                merged.sharedMesh = combined;
                var boneArray = Array(bones, finalBones.Count);
                for (int k = 0; k < boneArray.Length; k++) boneArray[k] = finalBones[k];
                merged.bones = boneArray;
                System.Array.Clear(boneArray, 0, boneArray.Length);
                var poseArray = Array(poses, finalPoses.Count);
                for (int k = 0; k < poseArray.Length; k++) poseArray[k] = finalPoses[k];
                merged.sharedMesh.bindposes = poseArray;
                var materialArray = Array(materialArrays, plan.Groups);
                for (int g = 0; g < plan.Groups; g++) materialArray[g] = materials[plan.Material(g)];
                merged.sharedMaterials = materialArray;
                System.Array.Clear(materialArray, 0, materialArray.Length);
                merged.rootBone = RootBone(combiner);
                var manager = result.AddComponent<CombinedMeshesManager>();
                manager.rootGameObject = combiner.gameObject;
                manager.mergeMethodUsed = CombinedMeshesManager.MergeMethod.OneMeshPerMaterial;
                var sourceArray = sources.ToArray();
                manager.sourceMeshes = sourceArray;
                var originals = new GameObject[items.Count];
                for (int i = 0; i < originals.Length; i++) originals[i] = items[i].gameObject;
                combiner.resultMergeOriginalGameObjects = originals;
                foreach (var source in sourceArray) source.gameObject.SetActive(false);
                animator.cullingMode = cullingMode;
                Fast++;
                if (combiner.onCombineMeshs != null) combiner.onCombineMeshs.Invoke();
            }
            catch (Exception error)
            {
                Failed++;
                LastError = error.GetType().Name + ": " + error.Message;
                foreach (var mesh in groupMeshes) if (mesh != null) Object.Destroy(mesh);
                groupMeshes.Clear();
                stop!(combiner, error);
            }
            if (combiner.autoManagePosition)
            {
                root.position = originalPosition!(combiner);
                root.eulerAngles = originalRotation!(combiner);
                root.localScale = originalScale!(combiner);
            }
        }

        private static Transform? RootBone(SkinnedMeshCombiner combiner)
        {
            if (combiner.rootBoneToUse == SkinnedMeshCombiner.RootBoneToUse.Manual) return combiner.manualRootBoneToUse;
            if (combiner.rootBoneToUse != SkinnedMeshCombiner.RootBoneToUse.Automatic) return null;
            roots.Clear(); rootCounts.Clear();
            foreach (var source in sources)
            {
                if (source == null || source.rootBone == null) continue;
                var bone = source.rootBone;
                int at = roots.IndexOf(bone);
                if (at < 0) { roots.Add(bone); rootCounts.Add(1); } else rootCounts[at]++;
            }
            Transform? best = null;
            int most = 0;
            for (int i = 0; i < roots.Count; i++) if (rootCounts[i] > most) { best = roots[i]; most = rootCounts[i]; }
            roots.Clear(); rootCounts.Clear();
            return best;
        }

        private static T[] Array<T>(Dictionary<int, T[]> cache, int length)
        {
            if (cache.TryGetValue(length, out var array)) return array;
            array = new T[length];
            if (cache.Count < MaximumCachedLengths) cache.Add(length, array);
            return array;
        }

        private static void Release()
        {
            plan.Clear(); found.Clear(); items.Clear(); sources.Clear(); sourceBones.Clear(); sourcePoses.Clear(); poseScratch.Clear();
            materialScratch.Clear(); materials.Clear(); finalBones.Clear(); finalPoses.Clear(); groupMeshes.Clear();
        }
    }
}
