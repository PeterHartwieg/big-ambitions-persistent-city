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
    internal sealed class RloTrafficScenes
    {
        public static readonly string[] Switches = { "TrafficAccidents", "NaturalAccidents" };

        private RloTrafficScenes(IReadOnlyList<RloSceneHold> holds, IReadOnlyList<string> unbound) { Holds = holds; Unbound = unbound; }

        public IReadOnlyList<RloSceneHold> Holds { get; }
        /// <summary>Switches that could not be bound, with the reason.</summary>
        public IReadOnlyList<string> Unbound { get; }

        public static RloTrafficScenes Bind(Assembly? rlo)
        {
            var holds = new List<RloSceneHold>();
            var unbound = new List<string>();
            if (rlo == null || rlo.GetName().Name != RloLivingPopulation.AssemblyName) return new RloTrafficScenes(holds, unbound);
            Type? settings;
            try { settings = rlo.GetType(RloLivingPopulation.SettingsTypeName, false); }
            catch (Exception error) { unbound.Add("all: " + error.Message); return new RloTrafficScenes(holds, unbound); }
            foreach (string name in Switches)
            {
                var field = settings?.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (field == null || field.FieldType != typeof(bool) || field.IsLiteral || field.IsInitOnly)
                { unbound.Add(name + ": missing or not a writable public static Boolean"); continue; }
                holds.Add(new RloSceneHold(name, () => Read(field), value => Write(field, value)));
            }
            return new RloTrafficScenes(holds, unbound);
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
