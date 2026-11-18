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
        private static readonly FieldInfo? PositionField = AccessTools.Field(typeof(SkinnedMeshCombiner), "thisOriginalPosition");
        private static readonly FieldInfo? RotationField = AccessTools.Field(typeof(SkinnedMeshCombiner), "thisOriginalRotation");
        private static readonly FieldInfo? ScaleField = AccessTools.Field(typeof(SkinnedMeshCombiner), "thisOriginalScale");
        private static Action<SkinnedMeshCombiner>? validate;
        private static AccessTools.FieldRef<SkinnedMeshCombiner, Vector3>? originalPosition, originalRotation, originalScale;
        private static bool hooked;
        private static readonly HashSet<SkinnedMeshCombiner> owned = new HashSet<SkinnedMeshCombiner>();
        private static readonly BodyMergePlan plan = new BodyMergePlan();
        private static readonly BodyMergeGate gate = new BodyMergeGate();
        private static readonly BodyMergeFallbacks fallbacks = new BodyMergeFallbacks();
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
        public static string LastCollectError { get; private set; } = "";
        public static string Fallbacks => fallbacks.Describe();
        public static bool Hooked => hooked;
        public static bool Disabled => !gate.Allows;
        public static int Owned => owned.Count;

        public static void Install()
        {
            owned.RemoveWhere(combiner => combiner == null);
            if (hooked || LastError.Length != 0) return;
            try
            {
                if (Target == null || ValidateMethod == null || PositionField == null || RotationField == null || ScaleField == null
                    || PositionField.FieldType != typeof(Vector3) || RotationField.FieldType != typeof(Vector3) || ScaleField.FieldType != typeof(Vector3))
                    throw new InvalidOperationException("Unexpected native body combiner contract.");
                if (Harmony.HasAnyPatches(Owner)) throw new InvalidOperationException("Another body merge hook owner is already registered.");
                validate = AccessTools.MethodDelegate<Action<SkinnedMeshCombiner>>(ValidateMethod);
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

        private static readonly HashSet<Mesh> mergedMeshes = new HashSet<Mesh>();
        private static readonly Predicate<Mesh> Destroyed = mesh => mesh == null;
        public static long ReleasedMeshes { get; private set; }
        public static long ReleasedVertices { get; private set; }

        public static void Release(SkinnedMeshCombiner? combiner)
        {
            if (ReferenceEquals(combiner, null)) return;
            owned.Remove(combiner!);
            if (combiner == null) return;
            var result = combiner.resultMergeGameObject;
            if (result == null) return;
            var renderer = result.GetComponent<SkinnedMeshRenderer>();
            var mesh = renderer != null ? renderer.sharedMesh : null;
            if (mesh == null) return;
            mergedMeshes.Remove(mesh);
            ReleasedMeshes++; ReleasedVertices += mesh.vertexCount;
            Object.Destroy(mesh);
        }

        public static int LiveMeshes(out long vertices)
        {
            mergedMeshes.RemoveWhere(Destroyed);
            vertices = 0;
            foreach (var mesh in mergedMeshes) vertices += mesh.vertexCount;
            return mergedMeshes.Count;
        }

        private static bool MergePrefix(SkinnedMeshCombiner __instance)
        {
            if (!owned.Contains(__instance)) return true;
            if (!gate.Allows) return Fallback(BodyMergeFallback.FastOff);
            try
            {
                BodyMergeFallback reason;
                try { reason = Collect(__instance); }
                catch (Exception error) { reason = BodyMergeFallback.CollectError; LastCollectError = error.GetType().Name + ": " + error.Message; }
                if (reason != BodyMergeFallback.None) return Fallback(reason);
                if (Merge(__instance)) return false;
                return Fallback(BodyMergeFallback.MergeError);
            }
            finally { Release(); }
        }

        private static bool Fallback(BodyMergeFallback reason)
        {
            Native++;
            fallbacks.Count(reason);
            return true;
        }

        private static BodyMergeFallback Collect(SkinnedMeshCombiner combiner)
        {
            Release();
            var blendshapes = (BodyMergeBlendshapes)(int)combiner.blendShapesSupport;
            List<GameObject>? ignore = combiner.gameObjectsToIgnore;
            var parameters = combiner.oneMeshPerMaterialParams;
            var reason = BodyMergeEligibility.Check(new BodyMergeSettings(Application.isPlaying, combiner.resultMergeGameObject != null, combiner.legacyAnimationSupport,
                blendshapes, combiner.mergeMethod == SkinnedMeshCombiner.MergeMethod.OneMeshPerMaterial, ignore != null,
                combiner.launchConsoleLogs, parameters != null, combiner.GetComponent<Animator>() != null));
            if (reason != BodyMergeFallback.None || ignore == null || parameters == null) return reason;
            combiner.gameObject.GetComponentsInChildren(combiner.combineInactives, found);
            foreach (var renderer in found)
                if (BodyMergeEligibility.Item(renderer.gameObject.GetComponent<CombinedMeshesManager>() != null, Ignored(ignore, renderer.gameObject)))
                    items.Add(renderer);
            foreach (var item in items)
            {
                var component = item.gameObject.GetComponent<SkinnedMeshRenderer>();
                bool present = component != null;
                var mesh = present ? component!.sharedMesh : null;
                bool hasMesh = mesh != null;
                if (present) component!.GetSharedMaterials(materialScratch); else materialScratch.Clear();
                bool nulls = false;
                foreach (var material in materialScratch) if (material == null) nulls = true;
                var candidate = new BodyMergeSource(present, present && component!.enabled, hasMesh, hasMesh ? mesh!.blendShapeCount : 0,
                    materialScratch.Count, hasMesh ? mesh!.subMeshCount : 0, nulls);
                if (BodyMergeEligibility.Source(candidate, blendshapes)) sources.Add(component!);
            }
            if (sources.Count < 1) return BodyMergeFallback.NoSources;
            var rootBone = sources[0].rootBone;
            bool differentRoots = false;
            foreach (var source in sources) if (rootBone != source.rootBone) { differentRoots = true; break; }
            if (differentRoots && parameters.mergeOnlyEqualRootBones) return BodyMergeFallback.DifferentRootBones;
            for (int r = 0; r < sources.Count; r++)
            {
                var source = sources[r];
                var mesh = source.sharedMesh;
                var rendererBones = source.bones;
                if (rendererBones == null) return BodyMergeFallback.BonesMissing;
                if (combiner.compatibilityMode)
                {
                    mesh.GetBindposes(poseScratch);
                    if (poseScratch.Count < rendererBones.Length) return BodyMergeFallback.ShortBindposes;
                    for (int k = 0; k < rendererBones.Length; k++) sourcePoses.Add(poseScratch[k]);
                }
                sourceBones.Add(rendererBones);
                plan.AddRenderer(rendererBones.Length);
                source.GetSharedMaterials(materialScratch);
                for (int j = 0; j < mesh.subMeshCount; j++) plan.AddSubmesh(r, j, MaterialKey(materialScratch[j]));
            }
            plan.Build();
            return plan.Groups > 0 ? BodyMergeFallback.None : BodyMergeFallback.NoGroups;
        }

        private static bool Ignored(List<GameObject> ignore, GameObject candidate)
        {
            foreach (var item in ignore) if (item == candidate) return true;
            return false;
        }

        private static int MaterialKey(Material material)
        {
            for (int i = 0; i < materials.Count; i++) if (ReferenceEquals(materials[i], material)) return i;
            materials.Add(material);
            return materials.Count - 1;
        }

        private static bool Merge(SkinnedMeshCombiner combiner)
        {
            var root = combiner.gameObject.transform;
            Vector3 position = root.position, rotation = root.eulerAngles, scale = root.localScale;
            Animator? animator = null;
            var cullingMode = AnimatorCullingMode.AlwaysAnimate;
            bool culled = false, moved = false, hidden = false;
            Mesh? combined = null;
            GameObject? result = null;
            try
            {
                if (combiner.autoManagePosition)
                {
                    originalPosition!(combiner) = position;
                    originalRotation!(combiner) = rotation;
                    originalScale!(combiner) = scale;
                    moved = true;
                    root.position = Vector3.zero;
                    root.eulerAngles = Vector3.zero;
                    root.localScale = Vector3.one;
                }
                combiner.resultMergeLogs.Clear();
                validate!(combiner);
                animator = combiner.GetComponent<Animator>();
                cullingMode = animator.cullingMode;
                culled = true;
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
                    groupMeshes.Add(mesh);
                    mesh.CombineMeshes(parts, mergeSubMeshes: true, useMatrices: true);
                    var groupPoses = Array(poses, finalPoses.Count - poseStart);
                    for (int k = 0; k < groupPoses.Length; k++) groupPoses[k] = finalPoses[poseStart + k];
                    mesh.bindposes = groupPoses;
                }
                var final = Array(combines, groupMeshes.Count);
                for (int g = 0; g < groupMeshes.Count; g++) final[g] = new CombineInstance { mesh = groupMeshes[g], subMeshIndex = 0, transform = Matrix4x4.identity };
                combined = new Mesh { indexFormat = format, name = "Combined Meshes (One Mesh Per Material)" };
                combined.CombineMeshes(final, mergeSubMeshes: false);
                DestroyGroups();
                combined.RecalculateBounds();
                mergedMeshes.Add(combined);
                result = new GameObject(combiner.nameOfThisMerge);
                result.transform.SetParent(root);
                var merged = result.AddComponent<SkinnedMeshRenderer>();
                merged.sharedMesh = combined;
                var boneArray = Array(bones, finalBones.Count);
                for (int k = 0; k < boneArray.Length; k++) boneArray[k] = finalBones[k];
                merged.bones = boneArray;
                var poseArray = Array(poses, finalPoses.Count);
                for (int k = 0; k < poseArray.Length; k++) poseArray[k] = finalPoses[k];
                combined.bindposes = poseArray;
                var materialArray = Array(materialArrays, plan.Groups);
                for (int g = 0; g < plan.Groups; g++) materialArray[g] = materials[plan.Material(g)];
                merged.sharedMaterials = materialArray;
                merged.rootBone = RootBone(combiner);
                var manager = result.AddComponent<CombinedMeshesManager>();
                manager.rootGameObject = combiner.gameObject;
                manager.mergeMethodUsed = CombinedMeshesManager.MergeMethod.OneMeshPerMaterial;
                var sourceArray = sources.ToArray();
                manager.sourceMeshes = sourceArray;
                var originals = new GameObject[items.Count];
                for (int i = 0; i < originals.Length; i++) originals[i] = items[i].gameObject;
                hidden = true;
                foreach (var source in sourceArray) source.gameObject.SetActive(false);
                combiner.resultMergeGameObject = result;
                combiner.resultMergeOriginalGameObjects = originals;
                if (combiner.onCombineMeshs != null) combiner.onCombineMeshs.Invoke();
                Fast++;
            }
            catch (Exception error)
            {
                Failed++;
                gate.Fail();
                LastError = error.GetType().Name + ": " + error.Message;
                Discard(combiner, combined, result, hidden);
                return false;
            }
            finally
            {
                ClearScratchArrays();
                if (culled && animator != null) animator.cullingMode = cullingMode;
                if (moved)
                {
                    root.position = position;
                    root.eulerAngles = rotation;
                    root.localScale = scale;
                }
            }
            return true;
        }

        private static void DestroyGroups()
        {
            foreach (var mesh in groupMeshes) if (mesh != null) Object.Destroy(mesh);
            groupMeshes.Clear();
        }

        private static void Discard(SkinnedMeshCombiner combiner, Mesh? combined, GameObject? result, bool hidden)
        {
            try { DestroyGroups(); } catch (Exception) { groupMeshes.Clear(); }
            try { if (combined != null) Object.Destroy(combined); } catch (Exception) { }
            if (result != null)
            {
                try { result.SetActive(false); result.transform.SetParent(null, false); } catch (Exception) { }
                try { Object.Destroy(result); } catch (Exception) { }
            }
            if (hidden) foreach (var source in sources) try { if (source != null) source.gameObject.SetActive(true); } catch (Exception) { }
            if (result != null && ReferenceEquals(combiner.resultMergeGameObject, result))
            {
                combiner.resultMergeGameObject = null!;
                combiner.resultMergeOriginalGameObjects = null!;
            }
        }

        private static void ClearScratchArrays()
        {
            foreach (var array in combines.Values) System.Array.Clear(array, 0, array.Length);
            foreach (var array in bones.Values) System.Array.Clear(array, 0, array.Length);
            foreach (var array in materialArrays.Values) System.Array.Clear(array, 0, array.Length);
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
