using UnityEngine;

namespace PersistentCity.Runtime
{
    internal static class ModUnityWork
    {
        public static long Instantiated { get; private set; }
        public static long Destroyed { get; private set; }
        public static long Activated { get; private set; }
        public static long AsyncStarted { get; private set; }
        public static long AsyncCompleted { get; private set; }
        public static long MeshRequests { get; private set; }

        public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, Transform parent) where T : Object
        {
            AsyncStarted++;
            return Object.InstantiateAsync(original, parent);
        }

        public static void AsyncDone() => AsyncCompleted++;

        public static void MeshRequest() => MeshRequests++;

        public static T Instantiate<T>(T original, Transform parent) where T : Object
        {
            Instantiated++;
            return Object.Instantiate(original, parent);
        }

        public static void Destroy(Object? target)
        {
            Destroyed++;
            Object.Destroy(target);
        }

        public static void DestroyImmediate(Object? target)
        {
            Destroyed++;
            Object.DestroyImmediate(target);
        }

        public static void Activate(GameObject target)
        {
            Activated++;
            target.SetActive(true);
        }
    }
}
