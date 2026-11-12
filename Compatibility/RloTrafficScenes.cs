using System;
using System.Collections.Generic;
using System.Reflection;
using PersistentCity.Core;

namespace PersistentCity.Compatibility
{
    /// <summary>
    /// Review Sol-r1-2: the RLO 1.0 switches of the street scenes that take over live native cars, bound once by plain
    /// reflection like <see cref="RloLivingPopulation"/>. <c>Settings.TrafficAccidents</c> gates staged accidents (and,
    /// with it, reckless drivers); <c>Settings.NaturalAccidents</c> gates reckless drivers, which also end a drive under way
    /// when it goes off. Writing the field is what RLO's own options toggle does; RLO stores options only from the options
    /// screen's callbacks, so the player's saved preference is not rewritten. Other RLO traffic features stay as they are.
    /// A missing or changed field binds nothing for that switch and is reported. Main thread only.
    /// </summary>
    /// Review Sol-r2-1: a staged accident already running when the hold starts is ended through RLO's public
    /// <c>TrafficAccidents.EndActive()</c> (its tick reads the switch only between scenes). Review Sol-r2-2: the release
    /// gives back the player's stored option, read through RLO's public <c>Settings.PrefsKey(id)</c> and the option-id
    /// constants, so an option switched off during the hold stays off.
    internal sealed class RloTrafficScenes
    {
        public static readonly string[] Switches = { "TrafficAccidents", "NaturalAccidents" };
        public const string AccidentsTypeName = "RealLifeOverhaul.TrafficAccidents";

        private readonly MethodInfo? endActive;

        private RloTrafficScenes(IReadOnlyList<RloSceneHold> holds, IReadOnlyList<string> unbound, MethodInfo? endActive)
        { Holds = holds; Unbound = unbound; this.endActive = endActive; }

        public IReadOnlyList<RloSceneHold> Holds { get; }
        /// <summary>Switches that could not be bound, with the reason.</summary>
        public IReadOnlyList<string> Unbound { get; }

        /// <summary><paramref name="readPrefs"/> answers an integer PlayerPrefs key, or null when it is not stored (Runtime
        /// passes UnityEngine.PlayerPrefs; Compatibility has no Unity reference).</summary>
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

        /// <summary>Ends a staged accident under way, once, as the hold starts. False when RLO's method is missing or threw.</summary>
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

        /// <summary>Starts or keeps every hold (at traffic hold and on the coarse poll). How many switches are held.</summary>
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
            catch (Exception) { } // a failed write leaves RLO's value; the next poll reads it again
        }
    }
}
