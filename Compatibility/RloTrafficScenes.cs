using System;
using System.Collections.Generic;
using System.Reflection;
using PersistentCity.Core;

namespace PersistentCity.Compatibility
{
    internal sealed class RloTrafficScenes
    {
        public static readonly string[] Switches = { "TrafficAccidents", "NaturalAccidents" };
        public const string AccidentsTypeName = "RealLifeOverhaul.TrafficAccidents";

        private readonly MethodInfo? endActive;

        private RloTrafficScenes(IReadOnlyList<RloSceneHold> holds, IReadOnlyList<string> unbound, MethodInfo? endActive)
        { Holds = holds; Unbound = unbound; this.endActive = endActive; }

        public IReadOnlyList<RloSceneHold> Holds { get; }
        public IReadOnlyList<string> Unbound { get; }

        public static RloTrafficScenes Bind(Assembly? rlo, Func<string, int?>? readPrefs = null)
        {
            var holds = new List<RloSceneHold>();
            var unbound = new List<string>();
            if (rlo == null || rlo.GetName().Name != RloLivingPopulation.AssemblyName) return new RloTrafficScenes(holds, unbound, null);
            Type? settings;
            MethodInfo? endActive = null, prefsKey = null;
            const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
            try
            {
                settings = rlo.GetType(RloLivingPopulation.SettingsTypeName, false);
                prefsKey = settings?.GetMethod("PrefsKey", PublicStatic, null, new[] { typeof(string) }, null);
                if (prefsKey != null && prefsKey.ReturnType != typeof(string)) prefsKey = null;
                endActive = rlo.GetType(AccidentsTypeName, false)?.GetMethod("EndActive", PublicStatic, null, Type.EmptyTypes, null);
                if (endActive == null) unbound.Add("TrafficAccidents.EndActive: missing; a staged accident under way finishes");
            }
            catch (Exception error) { unbound.Add("all: " + error.Message); return new RloTrafficScenes(holds, unbound, null); }
            foreach (string name in Switches)
            {
                var field = settings?.GetField(name, PublicStatic);
                if (field == null || field.FieldType != typeof(bool) || field.IsLiteral || field.IsInitOnly)
                { unbound.Add(name + ": missing or not a writable public static Boolean"); continue; }
                var id = settings!.GetField(name + "Id", PublicStatic);
                string? option = id != null && id.IsLiteral && id.FieldType == typeof(string) ? (string?)id.GetRawConstantValue() : null;
                Func<bool?>? saved = readPrefs == null || prefsKey == null || option == null ? null : () => Stored(prefsKey, option, readPrefs);
                holds.Add(new RloSceneHold(name, () => Read(field), value => Write(field, value), saved));
            }
            return new RloTrafficScenes(holds, unbound, endActive);
        }

        public bool EndActiveScene()
        {
            if (endActive == null) return false;
            try { endActive.Invoke(null, null); return true; }
            catch (Exception) { return false; }
        }

        private static bool? Stored(MethodInfo prefsKey, string option, Func<string, int?> readPrefs)
        {
            string? key = prefsKey.Invoke(null, new object[] { option }) as string;
            int? value = key == null ? null : readPrefs(key);
            return value.HasValue ? value.Value != 0 : (bool?)null;
        }

        public int Hold()
        {
            int held = 0;
            foreach (var hold in Holds) if (hold.Hold()) held++;
            return held;
        }

        public void Release() { foreach (var hold in Holds) hold.Release(); }

        public bool Holding { get { foreach (var hold in Holds) if (hold.Holding) return true; return false; } }

        private static bool? Read(FieldInfo field)
        {
            try { return field.GetValue(null) is bool value ? value : (bool?)null; }
            catch (Exception) { return null; }
        }

        private static void Write(FieldInfo field, bool value)
        {
            try { field.SetValue(null, value); }
            catch (Exception) { }
        }
    }
}
