using System;
using System.Collections.Generic;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>Live script inventory; only immutable managed type metadata is cached.</summary>
    internal static class VehicleScriptOwnership
    {
        // Queries still require Unity's main thread. Separate storage also keeps nested callers
        // from sharing a partially filled buffer; no vehicle or ownership verdict is retained.
        [ThreadStatic] private static Dictionary<Type, bool>? nativeTypes;
        [ThreadStatic] private static List<MonoBehaviour>? available;

        internal static bool NativeOnly(Component vehicle)
        {
            var scripts = available ?? new List<MonoBehaviour>(32);
            available = null;
            try
            {
                vehicle.GetComponentsInChildren(true, scripts);
                foreach (var script in scripts)
                    if (script != null && !NativeType(script.GetType())) return false;
                return true;
            }
            finally
            {
                scripts.Clear();
                // Retain one empty, modest buffer, even after nested calls or a large hierarchy.
                if (scripts.Capacity <= 2048 && available == null) available = scripts;
            }
        }

        private static bool NativeType(Type type)
        {
            var types = nativeTypes ?? (nativeTypes = new Dictionary<Type, bool>());
            if (types.TryGetValue(type, out bool native)) return native;
            string name = type.Assembly.GetName().Name!;
            native = name.StartsWith("BigAmbitions", StringComparison.Ordinal)
                || name == "ExternalPlugins" || name.StartsWith("Unity", StringComparison.Ordinal);
            types.Add(type, native);
            return native;
        }
    }
}
